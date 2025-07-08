import React, { useState, useEffect } from 'react';
import axios from 'axios';
import './App.css';

const API_BASE_URL = 'https://localhost:7000/api'; // Adjust port as needed

function App() {
  const [products, setProducts] = useState([]);
  const [newProduct, setNewProduct] = useState({ name: '', price: 0 });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchProducts();
  }, []);

  const fetchProducts = async () => {
    try {
      setLoading(true);
      const response = await axios.get(`${API_BASE_URL}/products`);
      setProducts(response.data);
    } catch (error) {
      console.error('Error fetching products:', error);
    } finally {
      setLoading(false);
    }
  };

  const createProduct = async (e) => {
    e.preventDefault();
    try {
      setLoading(true);
      await axios.post(`${API_BASE_URL}/products`, newProduct);
      setNewProduct({ name: '', price: 0 });
      fetchProducts(); // Refresh list
    } catch (error) {
      console.error('Error creating product:', error);
    } finally {
      setLoading(false);
    }
  };

  const triggerBackgroundJob = async () => {
    try {
      await axios.post(`${API_BASE_URL}/products/trigger-job`);
      alert('Background job triggered!');
    } catch (error) {
      console.error('Error triggering job:', error);
    }
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>My Full-Stack App</h1>
        
        {/* Create Product Form */}
        <form onSubmit={createProduct} style={{ margin: '20px 0' }}>
          <input
            type="text"
            placeholder="Product name"
            value={newProduct.name}
            onChange={(e) => setNewProduct({ ...newProduct, name: e.target.value })}
            required
          />
          <input
            type="number"
            step="0.01"
            placeholder="Price"
            value={newProduct.price}
            onChange={(e) => setNewProduct({ ...newProduct, price: parseFloat(e.target.value) })}
            required
          />
          <button type="submit" disabled={loading}>
            {loading ? 'Adding...' : 'Add Product'}
          </button>
        </form>

        {/* Background Job Trigger */}
        <button onClick={triggerBackgroundJob} style={{ margin: '10px' }}>
          Trigger Background Job
        </button>

        {/* Products List */}
        <div>
          <h2>Products</h2>
          {loading ? (
            <p>Loading...</p>
          ) : (
            <ul>
              {products.map((product) => (
                <li key={product.id}>
                  {product.name} - ${product.price}
                  <small> (Created: {new Date(product.createdAt).toLocaleDateString()})</small>
                </li>
              ))}
            </ul>
          )}
        </div>
      </header>
    </div>
  );
}

export default App;