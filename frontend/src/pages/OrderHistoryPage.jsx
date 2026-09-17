import { useEffect, useState } from "react";
import { ChevronDown, ChevronUp, CheckCircle2, XCircle, PackageOpen, Ban } from "lucide-react";
import { ordersApi } from "../api/client";
import { useCustomerSession } from "../context/CustomerSessionContext";
import ErrorBanner from "../components/ErrorBanner";
import DiscountBreakdown from "../components/DiscountBreakdown";

const STATUS_META = {
  CONFIRMED: { label: "CONFIRMED", Icon: CheckCircle2 },
  CANCELLED: { label: "CANCELLED", Icon: XCircle },
};

export default function OrderHistoryPage() {
  const { currentCustomerId } = useCustomerSession();
  const [orders, setOrders] = useState([]);
  const [expandedId, setExpandedId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!currentCustomerId) return;
    loadOrders();
  }, [currentCustomerId]);

  function loadOrders() {
    setLoading(true);
    ordersApi
      .get(`/api/orders?customerId=${currentCustomerId}`)
      .then(setOrders)
      .catch(setError)
      .finally(() => setLoading(false));
  }

  async function handleCancel(orderId) {
    setError(null);
    try {
      await ordersApi.post(`/api/orders/${orderId}/cancel`);
      loadOrders();
    } catch (err) {
      setError(err);
    }
  }

  if (loading) return <p>Cargando historial...</p>;

  return (
    <div className="page">
      <h1>Historial de pedidos</h1>
      <ErrorBanner error={error} />

      {orders.length === 0 && (
        <p className="empty-state">
          <PackageOpen size={18} />
          Este cliente todavía no tiene pedidos.
        </p>
      )}

      <ul className="order-list">
        {orders.map((order) => {
          const { label, Icon } = STATUS_META[order.status] ?? { label: order.status, Icon: PackageOpen };
          const isExpanded = expandedId === order.id;
          return (
            <li key={order.id} className="order-item">
              <div className="order-summary" onClick={() => setExpandedId(isExpanded ? null : order.id)}>
                <span>Pedido #{order.id}</span>
                <span className={`status status-${order.status.toLowerCase()}`}>
                  <Icon size={14} />
                  {label}
                </span>
                <span>{new Date(order.createdAt).toLocaleString("es-CO")}</span>
                <span>${Number(order.totalFinal).toLocaleString("es-CO")}</span>
                {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
              </div>

              {isExpanded && (
                <div className="order-detail">
                  <DiscountBreakdown order={order} />
                  {order.status === "CONFIRMED" && (
                    <button className="button-danger" onClick={() => handleCancel(order.id)}>
                      <Ban size={16} />
                      Cancelar pedido
                    </button>
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
