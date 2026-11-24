import './App.css';
import { useState } from 'react';
import Shop from './Shop';
import Orders from './Orders';

function App() {
  const [page, setPage] = useState('shop');

  return (
    <div>
      <nav>
        <button disabled={page === 'shop'} onClick={() => setPage('shop')}>SHOP</button>
        <button disabled={page === 'orders'} onClick={() => setPage('orders')}>ORDERS</button>
      </nav>
      {page === 'shop' ? <Shop /> : <Orders />}
    </div>
  );
}

export default App;
