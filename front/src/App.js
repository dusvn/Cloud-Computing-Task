import logo from './logo.svg';
import './App.css';
import { useState } from 'react';
function App() {

  const firstName = "Dusan";
  const lastName = "Paripovic";
  const email = "example@example.com"
  
  const [quantities, setQuantities] = useState({
    "Knjiga1": "",
    "Knjiga2": "",
    "Knjiga3": ""
  });
  

  const handleQuantityChange = (e, bookName) => {
    setQuantities({
      ...quantities,
      [bookName]: e.target.value
    });
  };

  const handleSubmit = async (bookName, price) => {
    const quantity = quantities[bookName];
    const orderData = {
      firstName,
      lastName,
      bookName,
      quantity: parseInt(quantity),
      email
    };

    try {
      const response = await fetch("http://localhost:8958/api/Client/Purchase", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(orderData)
      });

      const result = await response.json();
      console.log("Order Submitted:", result);
    } catch (error) {
      console.error("Error submitting order:", error);
    }
  };


  return (
    <table>
      <thead>
        <tr>
          <th>NAME</th>
          <th>PRICE</th>
          <th>NUMBER FOR ORDER</th>
          <th>ACTION</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>Knjiga 1</td>
          <td>10.26</td>
          <td>
            <input
              type="number"
              value={quantities["Knjiga1"]}
              onChange={(e) => handleQuantityChange(e, "Knjiga1")}
            />
          </td>
          <td>
            <button onClick={() => handleSubmit("Knjiga1", 10.26)}>SUBMIT</button>
          </td>
        </tr>
        <tr>
          <td>Knjiga 2</td>
          <td>26.10</td>
          <td>
            <input
              type="number"
              value={quantities["Knjiga2"]}
              onChange={(e) => handleQuantityChange(e, "Knjiga2")}
            />
          </td>
          <td>
            <button onClick={() => handleSubmit("Knjiga2", 26.10)}>SUBMIT</button>
          </td>
        </tr>
        <tr>
          <td>Knjiga 3</td>
          <td>15.24</td>
          <td>
            <input
              type="number"
              value={quantities["Knjiga3"]}
              onChange={(e) => handleQuantityChange(e, "Knjiga 3")}
            />
          </td>
          <td>
            <button onClick={() => handleSubmit("Knjiga3", 15.24)}>SUBMIT</button>
          </td>
        </tr>
      </tbody>
    </table>
  );
}

export default App;
