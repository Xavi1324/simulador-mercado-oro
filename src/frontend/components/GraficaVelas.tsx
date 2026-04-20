import { useEffect, useRef } from 'react';
import type { NuevoPrecioPayload, ApuestaEspeculativa } from '@/types/simulador';
import type { IChartApi, IPriceLine, ISeriesApi, UTCTimestamp } from 'lightweight-charts';

const COLORES_ESTRATEGIA: Record<string, string> = {
  Agresiva:     '#4ADE80',
  Conservadora: '#60A5FA',
  Tendencia:    '#F87171',
};

const TICKS_POR_VELA = 4;

interface GraficaVelasProps {
  onNuevoPrecioRef: React.MutableRefObject<((data: NuevoPrecioPayload) => void) | null>;
  predicciones: ApuestaEspeculativa[] | null;
  estrategiaSeleccionada: ApuestaEspeculativa | null;
  segsRestantes: number | null;
}

export default function GraficaVelas({
  onNuevoPrecioRef,
  predicciones,
  estrategiaSeleccionada,
  segsRestantes,
}: GraficaVelasProps) {

  const containerRef = useRef<HTMLDivElement>(null);
  const precioActualRef = useRef<HTMLSpanElement>(null);

  const ticksEnVelaActualRef = useRef<number[]>([]);

  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null);
  const lcRef = useRef<typeof import('lightweight-charts') | null>(null);
  const estratLineasRef = useRef<Map<string, IPriceLine>>(new Map());

  useEffect(() => {
    if (typeof window === 'undefined' || !containerRef.current) return;

    let observerRef: ResizeObserver | null = null;
    const lineasEstrategia = estratLineasRef.current;

    import('lightweight-charts').then((lc) => {
      if (!containerRef.current) return;

      lcRef.current = lc;

      const chart = lc.createChart(containerRef.current, {
        width: containerRef.current.clientWidth,
        height: containerRef.current.clientHeight || 520,

        layout: {
          background: { color: '#0f172a' },
          textColor: '#94a3b8',
        },

        grid: {
          vertLines: { color: '#1e293b' },
          horzLines: { color: '#1e293b' },
        },

        timeScale: {
          timeVisible: true,
          secondsVisible: true,

          // ✅ ESTE ES EL CORRECTO
          tickMarkFormatter: (time: number) => {
            return new Intl.DateTimeFormat('en-US', {
              hour: '2-digit',
              minute: '2-digit',
              second: '2-digit',
              hour12: false,
              timeZone: 'America/New_York',
            }).format(new Date(time * 1000));
          },
        },
      });

      const series = chart.addSeries(lc.CandlestickSeries, {
        upColor: '#22c55e',
        downColor: '#ef4444',
        borderVisible: false,
        wickUpColor: '#22c55e',
        wickDownColor: '#ef4444',
      });

      chartRef.current = chart;
      seriesRef.current = series;

      observerRef = new ResizeObserver(() => {
        if (containerRef.current && chartRef.current) {
          chartRef.current.applyOptions({
            width: containerRef.current.clientWidth,
            height: containerRef.current.clientHeight,
          });
        }
      });

      observerRef.observe(containerRef.current);

      onNuevoPrecioRef.current = (data: NuevoPrecioPayload) => {
        ticksEnVelaActualRef.current.push(data.precio);

        if (ticksEnVelaActualRef.current.length === TICKS_POR_VELA) {

          const precios = ticksEnVelaActualRef.current;
          const cierre = precios[TICKS_POR_VELA - 1];

          const vela = {
            time: Math.floor(Date.now() / 1000) as UTCTimestamp,
            open: precios[0],
            high: Math.max(...precios),
            low: Math.min(...precios),
            close: cierre,
          };

          if (precioActualRef.current) {
            precioActualRef.current.textContent = `$${cierre.toLocaleString('en-US', {
              minimumFractionDigits: 2,
              maximumFractionDigits: 2,
            })}`;
          }

          seriesRef.current?.update(vela);
          ticksEnVelaActualRef.current = [];
        }
      };
    });

    return () => {
      onNuevoPrecioRef.current = null;
      observerRef?.disconnect();
      chartRef.current?.remove();
      chartRef.current = null;
      seriesRef.current = null;
      lcRef.current = null;
      lineasEstrategia.clear();
    };
  }, [onNuevoPrecioRef]);

  useEffect(() => {
    const series = seriesRef.current;
    const lc = lcRef.current;
    if (!series || !lc) return;

    estratLineasRef.current.forEach((l) => {
      try { series.removePriceLine(l); } catch {}
    });

    estratLineasRef.current.clear();

    if (!predicciones) return;

    predicciones.forEach((est) => {
      const color = COLORES_ESTRATEGIA[est.nombre] ?? '#ffffff';
      const flecha =
        est.direccion === 'Alcista' ? '↑' :
        est.direccion === 'Bajista' ? '↓' : '→';

      if (est.precioMin != null && est.precioMax != null) {
        // Conservadora: dos líneas punteadas que marcan el rango
        const lineaMin = series.createPriceLine({
          price: est.precioMin,
          color,
          lineWidth: 1,
          lineStyle: lc.LineStyle.Dashed,
          axisLabelVisible: true,
          title: `${est.nombre} ↓`,
        });
        const lineaMax = series.createPriceLine({
          price: est.precioMax,
          color,
          lineWidth: 1,
          lineStyle: lc.LineStyle.Dashed,
          axisLabelVisible: true,
          title: `${est.nombre} ↑`,
        });
        estratLineasRef.current.set(`${est.nombre}_min`, lineaMin);
        estratLineasRef.current.set(`${est.nombre}_max`, lineaMax);
      } else {
        // Agresiva / Tendencia: línea única
        const linea = series.createPriceLine({
          price: est.precioEsperado,
          color,
          lineWidth: 2,
          lineStyle: lc.LineStyle.Solid,
          axisLabelVisible: true,
          title: `${est.nombre} ${flecha}`,
        });
        estratLineasRef.current.set(est.nombre, linea);
      }
    });
  }, [predicciones]);

  useEffect(() => {
    const series = seriesRef.current;
    if (!series || !estrategiaSeleccionada) return;

    estratLineasRef.current.forEach((linea, clave) => {
      // La clave puede ser "Conservadora_min", "Conservadora_max" o "Agresiva" etc.
      const nombreBase = clave.replace(/_min$|_max$/, '');
      if (nombreBase !== estrategiaSeleccionada.nombre) {
        try { series.removePriceLine(linea); } catch { /* ignorar */ }
        estratLineasRef.current.delete(clave);
      }
    });
  }, [estrategiaSeleccionada]);

  return (
    <div className="bg-slate-800 rounded-xl overflow-hidden border border-slate-700">

      <div className="relative">
        {/* Precio actual — arriba izquierda */}
        <div className="absolute top-3 left-4 z-10">
          <span
            ref={precioActualRef}
            className="text-3xl font-bold text-white tabular-nums"
          >
            —
          </span>
          <span className="ml-2 text-slate-400 text-sm">XAU/USD</span>
        </div>

        {/* Cronómetro de evaluación — arriba derecha */}
        {segsRestantes !== null && (
          <div className="absolute top-3 right-4 z-10 bg-slate-900/80 backdrop-blur-sm border border-yellow-500/40 rounded-lg px-3 py-1.5 flex items-center gap-2 pointer-events-none">
            <span className="text-slate-400 text-xs uppercase tracking-wider font-semibold">Eval</span>
            <span className="font-mono text-xl font-bold text-yellow-400 tabular-nums">
              {segsRestantes}s
            </span>
          </div>
        )}

        <div
          ref={containerRef}
          className="w-full"
          style={{ height: 'calc(100vh - 180px)' }}
        />
      </div>

      {predicciones && predicciones.length > 0 && (
        <div className="px-3 py-2 border-t border-slate-700 flex items-center gap-2 flex-wrap">

          <span className="text-slate-600 text-xs uppercase tracking-wider font-semibold mr-1">
            Estrategias
          </span>

          {predicciones.map((est) => {

            const seleccionada =
              estrategiaSeleccionada?.nombre === est.nombre;

            const color = COLORES_ESTRATEGIA[est.nombre] ?? '#fff';

            const flecha =
              est.direccion === 'Alcista' ? '↑' :
              est.direccion === 'Bajista' ? '↓' : '→';

            return (
              <div
                key={est.nombre}
                style={{ borderColor: color }}
                className={`border rounded-lg px-3 py-1 flex items-center gap-2 ${
                  seleccionada ? 'bg-slate-700' : 'bg-slate-800/60'
                }`}
              >
                <span className="text-xs font-bold" style={{ color }}>
                  {est.nombre} {flecha}
                </span>

                <span className="text-slate-200 font-mono text-xs font-semibold">
                  {est.precioMin != null && est.precioMax != null
                    ? `$${est.precioMin.toFixed(2)} — $${est.precioMax.toFixed(2)}`
                    : `$${est.precioEsperado.toFixed(2)}`}
                </span>

                {seleccionada && (
                  <span className="text-xs bg-green-700 text-green-100 px-1.5 py-0.5 rounded">
                    ✓
                  </span>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
