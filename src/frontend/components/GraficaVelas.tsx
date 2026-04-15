import { useEffect, useRef } from 'react';
import type { NuevoPrecioPayload } from '@/types/simulador';

const NIVELES_FIB = [0, 0.236, 0.382, 0.5, 0.618, 0.764, 1];
const COLORES_FIB = ['#94a3b8', '#f59e0b', '#10b981', '#3b82f6', '#10b981', '#f59e0b', '#94a3b8'];
const LABELS_FIB  = ['0%', '23.6%', '38.2%', '50%', '61.8%', '76.4%', '100%'];
const TICKS_POR_VELA = 4;
const VENTANA_HISTORIAL = 50;

interface GraficaVelasProps {
  onNuevoPrecioRef: React.MutableRefObject<((data: NuevoPrecioPayload) => void) | null>;
  modoFuente: 'Swissquote' | 'API' | 'CSV' | null;
}

export default function GraficaVelas({ onNuevoPrecioRef, modoFuente }: GraficaVelasProps) {
  const containerRef   = useRef<HTMLDivElement>(null);
  const precioActualRef = useRef<HTMLSpanElement>(null);

  const ticksEnVelaActualRef = useRef<number[]>([]);
  const historialPreciosRef  = useRef<number[]>([]);

  // Almacenar referencias tipadas como unknown para evitar imports de tipos del chart
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const chartRef     = useRef<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const seriesRef    = useRef<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const fibLineasRef = useRef<any[]>([]);

  useEffect(() => {
    if (typeof window === 'undefined' || !containerRef.current) return;

    let observerRef: ResizeObserver | null = null;

    import('lightweight-charts').then((lc) => {
      if (!containerRef.current) return;

      const chart = lc.createChart(containerRef.current, {
        width:  containerRef.current.clientWidth,
        height: 400,
        layout: {
          background: { color: '#0f172a' },
          textColor:  '#94a3b8',
        },
        grid: {
          vertLines: { color: '#1e293b' },
          horzLines: { color: '#1e293b' },
        },
        timeScale: { timeVisible: true, secondsVisible: true },
      });

      // API v5: addSeries(CandlestickSeries, opciones)
      const series = chart.addSeries(lc.CandlestickSeries, {
        upColor:      '#22c55e',
        downColor:    '#ef4444',
        borderVisible: false,
        wickUpColor:   '#22c55e',
        wickDownColor: '#ef4444',
      });

      chartRef.current  = chart;
      seriesRef.current = series;

      // Responsive width
      observerRef = new ResizeObserver(() => {
        if (containerRef.current && chartRef.current) {
          chartRef.current.applyOptions({ width: containerRef.current.clientWidth });
        }
      });
      observerRef.observe(containerRef.current);

      // Callback SignalR — se asigna al ref para que index.tsx lo pase a GraficaVelas
      onNuevoPrecioRef.current = (data: NuevoPrecioPayload) => {
        // Actualizar precio sin re-render
        if (precioActualRef.current) {
          precioActualRef.current.textContent = `$${data.precio.toLocaleString('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
          })}`;
        }

        ticksEnVelaActualRef.current.push(data.precio);
        historialPreciosRef.current.push(data.precio);
        if (historialPreciosRef.current.length > VENTANA_HISTORIAL) {
          historialPreciosRef.current.shift();
        }

        if (ticksEnVelaActualRef.current.length === TICKS_POR_VELA) {
          const precios = ticksEnVelaActualRef.current;
          const vela = {
            time:  Math.floor(Date.now() / 1000) as import('lightweight-charts').UTCTimestamp,
            open:  precios[0],
            high:  Math.max(...precios),
            low:   Math.min(...precios),
            close: precios[TICKS_POR_VELA - 1],
          };
          seriesRef.current?.update(vela);
          ticksEnVelaActualRef.current = [];

          // Recalcular Fibonacci sobre el historial de precios
          const hist = historialPreciosRef.current;
          if (hist.length >= 2 && seriesRef.current) {
            const min = Math.min(...hist);
            const max = Math.max(...hist);

            // Eliminar líneas anteriores
            fibLineasRef.current.forEach((linea) => {
              try { seriesRef.current?.removePriceLine(linea); } catch { /* ignorar */ }
            });
            fibLineasRef.current = [];

            // Crear nuevas líneas Fibonacci
            NIVELES_FIB.forEach((nivel, i) => {
              const precio = min + (max - min) * nivel;
              const linea = seriesRef.current.createPriceLine({
                price:            precio,
                color:            COLORES_FIB[i],
                lineWidth:        1,
                lineStyle:        lc.LineStyle.Dashed,
                axisLabelVisible: true,
                title:            `Fib ${LABELS_FIB[i]}`,
              });
              fibLineasRef.current.push(linea);
            });
          }
        }
      };
    });

    return () => {
      onNuevoPrecioRef.current = null;
      observerRef?.disconnect();
      chartRef.current?.remove();
      chartRef.current  = null;
      seriesRef.current = null;
      fibLineasRef.current = [];
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="bg-slate-800 rounded-xl overflow-hidden">
      <div className="relative">
        {/* Precio actual — esquina superior izquierda */}
        <div className="absolute top-3 left-4 z-10">
          <span
            ref={precioActualRef}
            className="text-3xl font-bold text-white tabular-nums"
          >
            —
          </span>
          <span className="ml-2 text-slate-400 text-sm">XAU/USD</span>
        </div>

        {/* Badge modo fuente — esquina superior derecha */}
        <div className="absolute top-3 right-4 z-10">
          {modoFuente === 'Swissquote' ? (
            <span className="bg-green-600 text-white text-xs font-semibold px-2 py-1 rounded-full">
              Swissquote 🟢
            </span>
          ) : modoFuente === 'API' ? (
            <span className="bg-blue-600 text-white text-xs font-semibold px-2 py-1 rounded-full">
              Metals-API 🔵
            </span>
          ) : modoFuente === 'CSV' ? (
            <span className="bg-yellow-600 text-white text-xs font-semibold px-2 py-1 rounded-full">
              Histórico CSV 🟡
            </span>
          ) : null}
        </div>

        {/* Contenedor del chart */}
        <div ref={containerRef} className="w-full" style={{ height: 400 }} />
      </div>
    </div>
  );
}
