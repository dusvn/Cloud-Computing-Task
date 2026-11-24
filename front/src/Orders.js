import { useCallback, useEffect, useState } from 'react';
import { API, USER } from './config';

const STATUS = ["Pending", "Completed", "Failed"];

function Orders() {
  const [orders, setOrders] = useState([]);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      const response = await fetch(`${API}/Orders?userId=${USER.userId}`);
      setOrders(await response.json());
      setError("");
    } catch (e) {
      setError(`Could not load orders: ${e}`);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <div>
      <button onClick={load}>REFRESH</button>
      {error && <div>{error}</div>}
      <table>
        <thead>
          <tr>
            <th>CREATED (UTC)</th><th>BOOK</th><th>QTY</th><th>AMOUNT</th>
            <th>STATUS</th><th>TRANSACTION ID</th><th>IDEMPOTENCY KEY</th>
          </tr>
        </thead>
        <tbody>
          {orders.map(o => (
            <tr key={o.idempotencyKey}>
              <td>{new Date(o.createdUtc).toISOString().replace("T", " ").slice(0, 19)}</td>
              <td>{o.bookName}</td>
              <td>{o.quantity}</td>
              <td>{o.amount.toFixed(2)}</td>
              <td>{typeof o.status === "number" ? STATUS[o.status] : o.status}</td>
              <td>{o.transactionId}</td>
              <td>{o.idempotencyKey}</td>
            </tr>
          ))}
          {orders.length === 0 && <tr><td colSpan="7">No orders yet.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}

export default Orders;
