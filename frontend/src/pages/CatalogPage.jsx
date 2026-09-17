import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PlusCircle, ShoppingCart, ArrowRight } from "lucide-react";
import { catalogApi } from "../api/client";
import { useOrderDraft } from "../context/OrderDraftContext";
import ErrorBanner from "../components/ErrorBanner";

const money = (n) => `$${Number(n).toLocaleString("es-CO")}`;

export default function CatalogPage() {
  const [products, setProducts] = useState([]);
  const [quantities, setQuantities] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const { addItem, items } = useOrderDraft();
  const navigate = useNavigate();

  useEffect(() => {
    catalogApi
      .get("/api/products")
      .then((data) => setProducts(data.data))
      .catch(setError)
      .finally(() => setLoading(false));
  }, []);

  function handleAdd(product) {
    const quantity = Math.max(1, Number(quantities[product.id]) || 1);
    addItem(product, quantity);
  }

  if (loading) return <p>Cargando catálogo...</p>;

  return (
    <div className="page">
      <h1>Catálogo</h1>
      <ErrorBanner error={error} />

      <table className="product-table">
        <thead>
          <tr>
            <th>Producto</th>
            <th>Precio</th>
            <th>Stock</th>
            <th>Cantidad</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {products.map((p) => (
            <tr key={p.id}>
              <td>{p.name}</td>
              <td>{money(p.price)}</td>
              <td>{p.stock}</td>
              <td>
                <input
                  type="number"
                  min="1"
                  max={p.stock}
                  value={quantities[p.id] ?? 1}
                  onChange={(e) => setQuantities({ ...quantities, [p.id]: e.target.value })}
                />
              </td>
              <td>
                <button onClick={() => handleAdd(p)} disabled={p.stock === 0}>
                  <PlusCircle size={16} />
                  Agregar
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {items.length > 0 && (
        <div className="draft-summary">
          <span className="draft-summary-label">
            <ShoppingCart size={16} />
            {items.length} producto(s) en el pedido en curso ({items.reduce((n, i) => n + i.quantity, 0)} unidades).
          </span>
          <button onClick={() => navigate("/crear-pedido")}>
            Ir a crear pedido
            <ArrowRight size={16} />
          </button>
        </div>
      )}
    </div>
  );
}
