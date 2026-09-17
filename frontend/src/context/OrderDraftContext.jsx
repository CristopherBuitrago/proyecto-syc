import { createContext, useContext, useState } from "react";

// Carrito compartido entre Catálogo y Crear Pedido. Un Context simple alcanza para
// este alcance (3 pantallas) — no se justifica una librería de estado (Redux/Zustand).
const OrderDraftContext = createContext(null);

export function OrderDraftProvider({ children }) {
  const [items, setItems] = useState([]); // [{ productId, name, unitPrice, quantity, availableStock }]
  const [couponCode, setCouponCode] = useState("");

  function addItem(product, quantity) {
    setItems((prev) => {
      const existing = prev.find((i) => i.productId === product.id);
      if (existing) {
        return prev.map((i) =>
          i.productId === product.id ? { ...i, quantity: i.quantity + quantity } : i
        );
      }
      return [
        ...prev,
        {
          productId: product.id,
          name: product.name,
          unitPrice: Number(product.price),
          quantity,
          availableStock: product.stock,
        },
      ];
    });
  }

  function updateQuantity(productId, quantity) {
    if (quantity <= 0) return removeItem(productId);
    setItems((prev) => prev.map((i) => (i.productId === productId ? { ...i, quantity } : i)));
  }

  function removeItem(productId) {
    setItems((prev) => prev.filter((i) => i.productId !== productId));
  }

  function clearDraft() {
    setItems([]);
    setCouponCode("");
  }

  return (
    <OrderDraftContext.Provider
      value={{ items, addItem, updateQuantity, removeItem, clearDraft, couponCode, setCouponCode }}
    >
      {children}
    </OrderDraftContext.Provider>
  );
}

export function useOrderDraft() {
  const ctx = useContext(OrderDraftContext);
  if (!ctx) throw new Error("useOrderDraft debe usarse dentro de OrderDraftProvider");
  return ctx;
}
