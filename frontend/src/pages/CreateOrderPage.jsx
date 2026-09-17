import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CheckCircle2, History, ShoppingCart, Trash2, Ticket, ArrowRight, PackageOpen, Layers } from "lucide-react";
import { ordersApi } from "../api/client";
import { useOrderDraft } from "../context/OrderDraftContext";
import { useCustomerSession } from "../context/CustomerSessionContext";
import ErrorBanner from "../components/ErrorBanner";
import DiscountBreakdown from "../components/DiscountBreakdown";

const money = (n) => `$${Number(n).toLocaleString("es-CO")}`;

// Descripción legible de un cupón, para que el cliente sepa qué está eligiendo sin
// tener que adivinar el significado de discountType/discountValue.
function describeCoupon(c) {
  const amount = c.discountType === "PERCENT" ? `${c.discountValue}%` : money(c.discountValue);
  const min = c.minPurchaseAmount > 0 ? ` · mín. ${money(c.minPurchaseAmount)}` : "";
  const stack = c.stackable ? " · combinable" : " · reemplaza otros descuentos si conviene más";
  return `${c.code} — ${amount}${min}${stack}`;
}

export default function CreateOrderPage() {
  const { items, updateQuantity, removeItem, clearDraft, couponCode, setCouponCode } = useOrderDraft();
  const { currentCustomer } = useCustomerSession();
  const [coupons, setCoupons] = useState([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [confirmedOrder, setConfirmedOrder] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    ordersApi.get("/api/coupons").then(setCoupons).catch(() => setCoupons([]));
  }, []);

  const subtotal = items.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      const order = await ordersApi.post("/api/orders", {
        customerId: currentCustomer.id,
        items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
        couponCode: couponCode || null,
      });
      setConfirmedOrder(order);
      clearDraft();
    } catch (err) {
      setError(err);
    } finally {
      setSubmitting(false);
    }
  }

  if (confirmedOrder) {
    return (
      <div className="page">
        <h1 className="page-title-success">
          <CheckCircle2 size={24} />
          Pedido confirmado #{confirmedOrder.id}
        </h1>
        <DiscountBreakdown order={confirmedOrder} />
        <div className="button-row">
          <button onClick={() => navigate("/historial")}>
            <History size={16} />
            Ver historial
          </button>
          <button onClick={() => setConfirmedOrder(null)}>
            <ShoppingCart size={16} />
            Crear otro pedido
          </button>
        </div>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="page">
        <h1>Crear pedido</h1>
        <p className="empty-state">
          <PackageOpen size={18} />
          Todavía no has agregado productos. Ve al catálogo para empezar.
        </p>
        <button onClick={() => navigate("/")}>
          Ir al catálogo
          <ArrowRight size={16} />
        </button>
      </div>
    );
  }

  return (
    <div className="page">
      <h1>Crear pedido</h1>
      <p className="current-customer">Pedido para: <strong>{currentCustomer?.name}</strong></p>

      <ErrorBanner error={error} />

      <table className="product-table">
        <thead>
          <tr>
            <th>Producto</th>
            <th>Precio</th>
            <th>Cantidad</th>
            <th>Subtotal línea</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {items.map((i) => (
            <tr key={i.productId}>
              <td>{i.name}</td>
              <td>{money(i.unitPrice)}</td>
              <td>
                <input
                  type="number"
                  min="1"
                  value={i.quantity}
                  onChange={(e) => updateQuantity(i.productId, Number(e.target.value))}
                />
              </td>
              <td>{money(i.unitPrice * i.quantity)}</td>
              <td>
                <button className="button-danger" onClick={() => removeItem(i.productId)}>
                  <Trash2 size={16} />
                  Quitar
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="coupon-field">
        <label htmlFor="coupon">
          <Ticket size={16} />
          Cupón (opcional)
        </label>
        <select id="coupon" value={couponCode} onChange={(e) => setCouponCode(e.target.value)}>
          <option value="">Sin cupón</option>
          {coupons.map((c) => (
            <option key={c.code} value={c.code}>
              {describeCoupon(c)}
            </option>
          ))}
        </select>
        {coupons.length === 0 && (
          <p className="coupon-empty-hint">
            <Layers size={14} />
            No hay cupones vigentes en este momento.
          </p>
        )}
      </div>

      <p className="subtotal-preview">Subtotal (sin descuentos): {money(subtotal)}</p>

      <button onClick={handleSubmit} disabled={submitting}>
        <CheckCircle2 size={16} />
        {submitting ? "Confirmando..." : "Confirmar pedido"}
      </button>
    </div>
  );
}
