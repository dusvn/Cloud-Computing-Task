using System.Fabric;
using BookstoreService.Database;
using BookstoreService.Saga;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace BookstoreService
{
    /// <summary>
    /// Saga participant for books. Reliable collections:
    ///   bookStore     BookId → Book            (stock)
    ///   heldStock     BookId → int             (books held by open reservations)
    ///   reservations  TxId   → Reservation     (one per saga; makes every command idempotent)
    ///   salesOutbox   queue of SaleEvent       (written in the same transaction as the stock removal)
    /// </summary>
    internal sealed class BookstoreService : StatefulService, IBookstoreService
    {
        private const string BooksName = "bookStore";
        private const string HeldStockName = "heldStock";
        private const string ReservationsName = "reservations";
        private const string OutboxName = "salesOutbox";
        private const int MaxBooksToLoad = 10;

        private readonly GetBookQueries _get;
        private readonly PostBookQueries _post;

        public BookstoreService(StatefulServiceContext context)
            : base(context)
        {
            string connectionString = DatabaseSettings.GetConnectionString(context);
            _get = new GetBookQueries(connectionString);
            _post = new PostBookQueries(connectionString);
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners() =>
            this.CreateServiceRemotingReplicaListeners();

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await InitBooks(cancellationToken);
            await PumpOutboxAsync(cancellationToken);
        }

        // ---------------- saga commands ----------------

        public async Task<StepResult> ReserveAsync(Guid txId, Purchase purchase)
        {
            var books = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>(BooksName);
            var held = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, int>>(HeldStockName);
            var reservations = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Reservation>>(ReservationsName);

            using var tx = StateManager.CreateTransaction();

            var existing = await reservations.TryGetValueAsync(tx, txId, LockMode.Update);
            if (existing.HasValue)   // repeated command: same answer as the first time
            {
                return existing.Value.Status == ReservationStatus.Released
                    ? StepResult.Fail("Reservation was rejected or already released.")
                    : StepResult.Ok("Books already reserved.", existing.Value.Amount);
            }

            var book = await books.TryGetValueAsync(tx, purchase.BookId, LockMode.Update);
            if (!book.HasValue)
                return await RejectAsync(tx, reservations, txId, purchase.BookId, "Book does not exist.");

            int heldNow = (await held.TryGetValueAsync(tx, purchase.BookId, LockMode.Update)).Value;
            if (book.Value.Quantity - heldNow < purchase.Quantity)
                return await RejectAsync(tx, reservations, txId, purchase.BookId, "Not enough books in stock.");

            var reservation = new Reservation
            {
                TxId = txId,
                BookId = purchase.BookId,
                UserId = purchase.UserId,
                BookName = book.Value.BookName,
                Email = purchase.Email,
                Quantity = purchase.Quantity,
                Amount = book.Value.Price * purchase.Quantity,
                Status = ReservationStatus.Reserved
            };
            await held.SetAsync(tx, purchase.BookId, heldNow + purchase.Quantity);
            await reservations.AddAsync(tx, txId, reservation);
            await tx.CommitAsync();
            return StepResult.Ok("Books reserved.", reservation.Amount);
        }

        /// <summary>
        /// A rejected reservation is remembered as Released (tombstone): a delayed duplicate of the same
        /// command then fails too, instead of holding books for a saga that has already ended.
        /// </summary>
        private static async Task<StepResult> RejectAsync(
            Microsoft.ServiceFabric.Data.ITransaction tx, IReliableDictionary<Guid, Reservation> reservations,
            Guid txId, Guid bookId, string reason)
        {
            await reservations.AddAsync(tx, txId, new Reservation { TxId = txId, BookId = bookId, Status = ReservationStatus.Released });
            await tx.CommitAsync();
            return StepResult.Fail(reason);
        }

        public async Task<StepResult> ConfirmAsync(Guid txId)
        {
            var books = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>(BooksName);
            var held = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, int>>(HeldStockName);
            var reservations = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Reservation>>(ReservationsName);
            var outbox = await StateManager.GetOrAddAsync<IReliableQueue<SaleEvent>>(OutboxName);

            using var tx = StateManager.CreateTransaction();

            var r = await reservations.TryGetValueAsync(tx, txId, LockMode.Update);
            if (!r.HasValue || r.Value.Status == ReservationStatus.Released)
                return StepResult.Fail("No open reservation for this transaction.");
            if (r.Value.Status == ReservationStatus.Confirmed)
                return StepResult.Ok("Sale already confirmed.");   // repeated command

            Reservation reservation = r.Value;
            Book book = (await books.TryGetValueAsync(tx, reservation.BookId, LockMode.Update)).Value;
            int heldNow = (await held.TryGetValueAsync(tx, reservation.BookId, LockMode.Update)).Value;

            // ONE transaction: stock -= quantity, reservation closed, sale written to the outbox
            book.Quantity -= reservation.Quantity;
            reservation.Status = ReservationStatus.Confirmed;
            await books.SetAsync(tx, book.BookId, book);
            await held.SetAsync(tx, book.BookId, heldNow - reservation.Quantity);
            await reservations.SetAsync(tx, txId, reservation);
            await outbox.EnqueueAsync(tx, new SaleEvent
            {
                PurchaseId = txId,
                BookId = book.BookId,
                UserId = reservation.UserId,
                BookName = book.BookName,
                Quantity = reservation.Quantity,
                Amount = reservation.Amount,
                Email = reservation.Email,
                RemainingStock = book.Quantity,
                SoldAtUtc = DateTime.UtcNow
            });
            await tx.CommitAsync();
            return StepResult.Ok("Sale confirmed.");
        }

        public async Task<StepResult> ReleaseAsync(Guid txId, Guid bookId)
        {
            var held = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, int>>(HeldStockName);
            var reservations = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Reservation>>(ReservationsName);

            using var tx = StateManager.CreateTransaction();

            var r = await reservations.TryGetValueAsync(tx, txId, LockMode.Update);
            if (!r.HasValue)
            {
                // Release before Reserve (Reserve timed out / still on its way): tombstone, a late Reserve will do nothing
                await reservations.AddAsync(tx, txId, new Reservation { TxId = txId, BookId = bookId, Status = ReservationStatus.Released });
                await tx.CommitAsync();
                return StepResult.Ok("Nothing was reserved; tombstone written.");
            }
            if (r.Value.Status == ReservationStatus.Released) return StepResult.Ok("Already released.");
            if (r.Value.Status == ReservationStatus.Confirmed) return StepResult.Fail("Sale is already confirmed and cannot be released.");

            Reservation reservation = r.Value;
            int heldNow = (await held.TryGetValueAsync(tx, reservation.BookId, LockMode.Update)).Value;
            reservation.Status = ReservationStatus.Released;
            await held.SetAsync(tx, reservation.BookId, heldNow - reservation.Quantity);
            await reservations.SetAsync(tx, txId, reservation);
            await tx.CommitAsync();
            return StepResult.Ok("Books released.");
        }

        // ---------------- startup + outbox ----------------

        /// <summary>Loads the first <see cref="MaxBooksToLoad"/> books from Postgres (public.books) into the reliable dictionary.</summary>
        private async Task InitBooks(CancellationToken cancellationToken)
        {
            var books = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>(BooksName);
            var ownRange = await PartitionHelper.GetOwnKeyRangeAsync(Context);
            List<Book> booksFromDatabase = await _get.GetBooksAsync(MaxBooksToLoad, cancellationToken);
            using var tx = StateManager.CreateTransaction();
            // each partition stores only the books whose MurmurHash3(bookId) falls in its key range
            foreach (var b in booksFromDatabase.Where(b => PartitionHelper.IsInRange(b.BookId, ownRange)))
                await books.TryAddAsync(tx, b.BookId, b);   // TryAdd: safe on failover/restart
            await tx.CommitAsync();
        }

        /// <summary>
        /// Outbox pump: every SaleEvent is delivered at-least-once to Postgres (new stock) and ReportService,
        /// and dequeued only after both succeeded. Both consumers are idempotent, so a redelivery is harmless.
        /// </summary>
        private async Task PumpOutboxAsync(CancellationToken cancellationToken)
        {
            var outbox = await StateManager.GetOrAddAsync<IReliableQueue<SaleEvent>>(OutboxName);
            var reportService = ServiceProxy.Create<IReportService>(new Uri("fabric:/CloudTask/ReportService"));
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var tx = StateManager.CreateTransaction();
                    var next = await outbox.TryPeekAsync(tx);
                    if (next.HasValue)
                    {
                        await _post.UpdateBookQuantityAsync(next.Value.BookId, next.Value.RemainingStock, cancellationToken);
                        await reportService.ConsumeSaleAsync(next.Value);
                        await outbox.TryDequeueAsync(tx);
                        await tx.CommitAsync();
                        continue;   // more messages may be waiting
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ServiceEventSource.Current.ServiceMessage(Context, "Outbox delivery failed, will retry: {0}", e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
