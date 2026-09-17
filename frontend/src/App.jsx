import { Routes, Route } from "react-router-dom";
import Header from "./components/Header";
import CatalogPage from "./pages/CatalogPage";
import CreateOrderPage from "./pages/CreateOrderPage";
import OrderHistoryPage from "./pages/OrderHistoryPage";

export default function App() {
  return (
    <>
      <Header />
      <main>
        <Routes>
          <Route path="/" element={<CatalogPage />} />
          <Route path="/crear-pedido" element={<CreateOrderPage />} />
          <Route path="/historial" element={<OrderHistoryPage />} />
        </Routes>
      </main>
    </>
  );
}
