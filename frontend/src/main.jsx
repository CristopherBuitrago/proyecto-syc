import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App.jsx";
import { CustomerSessionProvider } from "./context/CustomerSessionContext.jsx";
import { OrderDraftProvider } from "./context/OrderDraftContext.jsx";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <BrowserRouter>
      <CustomerSessionProvider>
        <OrderDraftProvider>
          <App />
        </OrderDraftProvider>
      </CustomerSessionProvider>
    </BrowserRouter>
  </React.StrictMode>
);
