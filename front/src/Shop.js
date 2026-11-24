import { useRef, useState } from 'react';
import { API, BOOKS, USER } from './config';

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

  const send = async (order) => {
    setLastOrder(order);
    try {
      const response = await fetch(`${API}/Purchase`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(order)
      });
      const body = await response.json();
      setResult({ status: response.status, ...body });
    } catch (error) {
      setResult({ status: 0, message: `Error submitting order: ${error}` });
    }
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
          <div><b>Message:</b> {result.message}</div>
          {result.transactionId && <div><b>Transaction ID:</b> {result.transactionId}</div>}
          {result.idempotencyKey && <div><b>Idempotency key:</b> {result.idempotencyKey}</div>}
          {lastOrder && (
            <button style={{ marginTop: 8 }} onClick={() => send(lastOrder)}>
              RESEND SAME REQUEST (same idempotency key)
            </button>
          )}
        </div>
      )}
    </div>
  );
}

export default Shop;
