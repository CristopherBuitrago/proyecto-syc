import { NavLink } from "react-router-dom";
import { LayoutGrid, ShoppingCart, History, User, PackageSearch } from "lucide-react";
import { useCustomerSession } from "../context/CustomerSessionContext";

export default function Header() {
  const { customers, currentCustomerId, setCurrentCustomerId, loading } = useCustomerSession();

  return (
    <header className="header">
      <div className="header-brand">
        <PackageSearch size={20} />
        Pedidos y Descuentos
      </div>

      <nav className="header-nav">
        <NavLink to="/" end>
          <LayoutGrid size={16} />
          Catálogo
        </NavLink>
        <NavLink to="/crear-pedido">
          <ShoppingCart size={16} />
          Crear pedido
        </NavLink>
        <NavLink to="/historial">
          <History size={16} />
          Historial
        </NavLink>
      </nav>

      <div className="header-session">
        <User size={16} />
        <label htmlFor="customer-select">Ingresando como:</label>
        <select
          id="customer-select"
          disabled={loading}
          value={currentCustomerId ?? ""}
          onChange={(e) => setCurrentCustomerId(Number(e.target.value))}
        >
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </div>
    </header>
  );
}
