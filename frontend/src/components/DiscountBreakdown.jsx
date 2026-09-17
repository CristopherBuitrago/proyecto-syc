import { Layers, Award, Ticket, Receipt } from "lucide-react";

const money = (n) => `$${Number(n).toLocaleString("es-CO")}`;

const DISCOUNT_META = {
  VOLUME: { label: "Descuento por volumen", Icon: Layers },
  LOYALTY: { label: "Descuento por nivel de cliente", Icon: Award },
  COUPON: { label: "Cupón", Icon: Ticket },
};

// Muestra exactamente lo que el motor decidió aplicar (AppliedDiscounts), no lo que el
// usuario intentó — así un cupón no-stackable que perdió la comparación simplemente no
// aparece acá, en vez de mostrar un descuento que nunca se aplicó (ver DECISIONS.md, punto 4).
export default function DiscountBreakdown({ order }) {
  return (
    <div className="discount-breakdown">
      <div className="discount-breakdown-title">
        <Receipt size={16} />
        <span>Desglose</span>
      </div>

      <div className="discount-row">
        <span>Subtotal</span>
        <span>{money(order.subtotal)}</span>
      </div>
      {order.appliedDiscounts.map((d, i) => {
        const meta = DISCOUNT_META[d.type];
        const Icon = meta?.Icon ?? Ticket;
        return (
          <div className="discount-row discount-applied" key={i}>
            <span className="discount-label">
              <Icon size={14} />
              {meta?.label ?? d.type}
            </span>
            <span>-{money(d.amount)}</span>
          </div>
        );
      })}
      <div className="discount-row discount-total">
        <span>Total</span>
        <span>{money(order.totalFinal)}</span>
      </div>
    </div>
  );
}
