import { useEffect, useRef } from 'react';
import type { NuevoPrecioPayload, ApuestaDemo } from '@/types/simulador';

const COLORES_ESTRATEGIA: Record<string, string> = {
  Agresiva:     '#4ADE80', // verde
  Conservadora: '#60A5FA', // azul
  Tendencia:    '#F87171', // rojo claro
};

const TICKS_POR_VELA = 4;

interface GraficaVelasProps {
  onNuevoPrecioRef:       React.MutableRefObject<((data: NuevoPrecioPayload) => void) | null>;
  modoFuente:             'Swissquote' | 'API' | 'CSV' | null;
  predicciones:           ApuestaDemo[] | null;
  estrategiaSeleccionada: ApuestaDemo | null;
}

export default function GraficaVelas({
  onNuevoPrecioRef,
  modoFuente,
  predicciones,
  estrategiaSeleccionada,
}: GraficaVelasProps) {
  const containerRef    = useRef<HTMLDivElement>(null);
  const precioActualRef = useRef<HTMLSpanElement>(null);

  const ticksEnVelaActualRef = useRef<number[]>([]);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const chartRef        = useRef<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const seriesRef       = useRef<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const lcRef           = useRef<any>(null);          // módulo lightweight-charts
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const estratLineasRef = useRef<Map<string, any>>(new Map()); // nombre → PriceLine

  // ── Inicializar chart ─────────────────────────────────────────────────────
  useEffect(() => {
    if (typeof window === 'undefined' || !containerRef.current) return;

    let observerRef: ResizeObserver | null = null;

    import('lightweight-charts').then((lc) => {
      if (!containerRef.current) return;
      lcRef.current = lc;

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

      const series = chart.addSeries(lc.CandlestickSeries, {
        upColor:       '#22c55e',
        downColor:     '#ef4444',
        borderVisible: false,
        wickUpColor:   '#22c55e',
        wickDownColor: '#ef4444',
      });

      chartRef.current  = chart;
      seriesRef.current = series;

      observerRef = new ResizeObserver(() => {
        if (containerRef.current && chartRef.current) {
          chartRef.current.applyOptions({ width: containerRef.current.clientWidth });
        }
      });
      observerRef.observe(containerRef.current);

      onNuevoPrecioRef.current = (data: NuevoPrecioPayload) => {
        if (precioActualRef.current) {
          precioActualRef.current.textContent = `$${data.precio.toLocaleString('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
          })}`;
        }

        ticksEnVelaActualRef.current.push(data.precio);

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
        }
      };
    });

    return () => {
      onNuevoPrecioRef.current = null;
      observerRef?.disconnect();
      chartRef.current?.remove();
      chartRef.current     = null;
      seriesRef.current    = null;
      lcRef.current        = null;
      estratLineasRef.current.clear();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Dibujar las 3 líneas de estrategia cuando llegan predicciones ─────────
  useEffect(() => {
    if (!seriesRef.current || !lcRef.current) return;

    // Borrar líneas anteriores
    estratLineasRef.current.forEach((l) => {
      try { seriesRef.current?.removePriceLine(l); } catch { /* ignorar */ }
    });
    estratLineasRef.current.clear();

    if (!predicciones) return;

    predicciones.forEach((est) => {
      const color = COLORES_ESTRATEGIA[est.nombre] ?? '#ffffff';
      const linea = seriesRef.current.createPriceLine({
        price:            est.precioEsperado,
        color,
        lineWidth:        2,
        lineStyle:        lcRef.current.LineStyle.Solid,
        axisLabelVisible: true,
        title:            `${est.nombre} ${est.direccion === 'Alcista' ? '↑' : est.direccion === 'Bajista' ? '↓' : '→'}`,
      });
      estratLineasRef.current.set(est.nombre, linea);
    });
  }, [predicciones]);

  // ── Eliminar las descartadas cuando se elige la ganadora ──────────────────
  useEffect(() => {
    if (!seriesRef.current || !estrategiaSeleccionada) return;

    estratLineasRef.current.forEach((linea, nombre) => {
      if (nombre !== estrategiaSeleccionada.nombre) {
        try { seriesRef.current?.removePriceLine(linea); } catch { /* ignorar */ }
        estratLineasRef.current.delete(nombre);
      }
    });
  }, [estrategiaSeleccionada]);

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="bg-slate-800 rounded-xl overflow-hidden">
      <div className="relative">
        {/* Precio actual */}
        <div className="absolute top-3 left-4 z-10">
          <span ref={precioActualRef} className="text-3xl font-bold text-white tabular-nums">—</span>
          <span className="ml-2 text-slate-400 text-sm">XAU/USD</span>
        </div>

        {/* Badge modo fuente */}
        <div className="absolute top-3 right-4 z-10">
          {modoFuente === 'Swissquote' && (
            <span className="bg-green-600 text-white text-xs font-semibold px-2 py-1 rounded-full">Swissquote 🟢</span>
          )}
          {modoFuente === 'API' && (
            <span className="bg-blue-600 text-white text-xs font-semibold px-2 py-1 rounded-full">Metals-API 🔵</span>
          )}
          {modoFuente === 'CSV' && (
            <span className="bg-yellow-600 text-white text-xs font-semibold px-2 py-1 rounded-full">Histórico CSV 🟡</span>
          )}
        </div>

        <div ref={containerRef} className="w-full" style={{ height: 400 }} />
      </div>

      {/* ── Tarjetas de estrategia ── */}
      {predicciones && predicciones.length > 0 && (
        <div className="px-4 pb-4 pt-3 border-t border-slate-700">
          <p className="text-xs text-slate-500 mb-2 uppercase tracking-wider font-semibold">
            Estrategias especulativas
          </p>
          <div className="flex gap-3 flex-wrap">
            {predicciones.map((est) => {
              const descartada =
                estrategiaSeleccionada !== null &&
                est.nombre !== estrategiaSeleccionada.nombre;
              const seleccionada =
                estrategiaSeleccionada?.nombre === est.nombre;
              const color = COLORES_ESTRATEGIA[est.nombre] ?? '#fff';

              return (
                <div
                  key={est.nombre}
                  style={{
                    borderColor: color,
                    opacity: descartada ? 0 : 1,
                    transition: 'opacity 0.6s ease-out',
                  }}
                  className={`border-2 rounded-lg px-4 py-2 min-w-[140px] ${
                    seleccionada ? 'bg-slate-700' : 'bg-slate-800'
                  }`}
                >
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs font-bold" style={{ color }}>
                      {est.nombre}
                    </span>
                    {seleccionada && (
                      <span className="text-xs bg-green-700 text-green-100 px-1.5 py-0.5 rounded ml-2">
                        ELEGIDA
                      </span>
                    )}
                  </div>
                  <p className="text-slate-100 font-mono text-sm font-semibold">
                    ${est.precioEsperado.toFixed(2)}
                  </p>
                  <p className="text-slate-400 text-xs mt-0.5">
                    {est.direccion} · {est.tiempoExpiracion}
                  </p>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
