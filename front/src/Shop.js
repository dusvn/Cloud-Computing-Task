import { useRef, useState } from 'react';
import { BOOKS, PURCHASES_API, USER } from './config';

const FINAL_STATES = ["Completed", "Compensated", "Failed"];
const POLL_ATTEMPTS = 30;
const POLL_INTERVAL_MS = 500;

// crypto.randomUUID needs a secure context; localhost is one, the fallback covers plain http hosts.
const newKey = () =>
  window.crypto?.randomUUID
    ? window.crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = (Math.random() * 16) | 0;
        return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
      });

function Shop() {
  const [quantities, setQuantities] = useState({});
  const [result, setResult] = useState(null);      // last server answer
  const [lastOrder, setLastOrder] = useState(null); // last request body, so it can be re-sent with the SAME key

  // One idempotency key per book while a request is in flight: a double click re-uses it,
  // a new click after the answer arrived gets a fresh key (= a new order).
  const inFlightKeys = useRef({});

  // POST returns 202 + Location at once; the saga runs in the background, so follow it with GET until it ends
  const send = async (order) => {
    setLastOrder(order);
    const { idempotencyKey, ...purchase } = order;
    try {
      const response = await fetch(PURCHASES_API, {
        method: "POST",
        headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
        body: JSON.stringify(purchase)
      });
      const body = await response.json().catch(() => ({ message: response.statusText }));
      setResult({ status: response.status, ...body });
      const location = response.headers.get("Location");
      if (response.status === 202 && location) await follow(location, response.status, body.isDuplicate);
    } catch (error) {
      setResult({ status: 0, message: `Error submitting order: ${error}` });
    }
  };

  const follow = async (location, status, isDuplicate) => {
    for (let i = 0; i < POLL_ATTEMPTS; i++) {
      const saga = await (await fetch(location)).json();
      setResult({ status, isDuplicate, state: saga.status, message: saga.message,
                  transactionId: saga.transactionId, idempotencyKey: saga.idempotencyKey });
      if (FINAL_STATES.includes(saga.status)) return;
      await new Promise(resolve => setTimeout(resolve, POLL_INTERVAL_MS));
    }
  };

  const cancel = async (order) => {
    const response = await fetch(`${PURCHASES_API}/${order.idempotencyKey}`, { method: "DELETE" });
    const body = await response.json().catch(() => ({ message: response.statusText }));
    setResult({ status: response.status, ...body });
    if (response.status === 202) await follow(`${PURCHASES_API}/${order.idempotencyKey}`, response.status, false);
  };

  const handleBuy = async (book) => {
    const quantity = parseInt(quantities[book.name]);
    if (!inFlightKeys.current[book.bookId]) inFlightKeys.current[book.bookId] = newKey();
    const order = {
      idempotencyKey: inFlightKeys.current[book.bookId],
      userId: USER.userId,
      bookId: book.bookId,
      firstName: USER.firstName,
      lastName: USER.lastName,
      bookName: book.name,
      quantity,
      email: USER.email
    };
    try { await send(order); }
    finally { delete inFlightKeys.current[book.bookId]; }
  };

  return (
    <div>
      <table>
        <thead>
          <tr><th>NAME</th><th>PRICE</th><th>NUMBER FOR ORDER</th><th>ACTION</th></tr>
        </thead>
        <tbody>
          {BOOKS.map(book => (
            <tr key={book.bookId}>
              <td>{book.label}</td>
              <td>{book.price.toFixed(2)}</td>
              <td>
                <input
                  type="number"
                  value={quantities[book.name] ?? ""}
                  onChange={(e) => setQuantities({ ...quantities, [book.name]: e.target.value })}
                />
              </td>
              <td><button onClick={() => handleBuy(book)}>BUY</button></td>
            </tr>
          ))}
        </tbody>
      </table>

      {result && (
        <div style={{ marginTop: 16 }}>
          <div><b>Status:</b> {result.status} {result.isDuplicate ? "(duplicate request - nothing was bought twice)" : ""}</div>
          {result.state && <div><b>Saga state:</b> {result.state}</div>}
          <div><b>Message:</b> {result.message}</div>
          {result.transactionId && <div><b>Transaction ID:</b> {result.transactionId}</div>}
          {result.idempotencyKey && <div><b>Idempotency key:</b> {result.idempotencyKey}</div>}
          {lastOrder && (
            <button style={{ marginTop: 8 }} onClick={() => send(lastOrder)}>
              RESEND SAME REQUEST (same idempotency key)
            </button>
          )}
          {lastOrder && (
            <button style={{ marginTop: 8, marginLeft: 8 }} onClick={() => cancel(lastOrder)}>
              CANCEL PURCHASE
            </button>
          )}
        </div>
      )}
    </div>
  );
}

export default Shop;
