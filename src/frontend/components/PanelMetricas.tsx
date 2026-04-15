import { useEffect, useState } from 'react';
import type { NuevaMetricaPayload, MetricaCiclo } from '@/types/simulador';

interface PanelMetricasProps {
  onNuevaMetricaRef: React.MutableRefObject<((data: NuevaMetricaPayload) => void) | null>;
  metricasIniciales: MetricaCiclo[];
}

function SpeedupChart({ datos }: { datos: NuevaMetricaPayload[] }) {
  if (datos.length < 2) return null;
  const maxSpeedup = Math.max(...datos.map((d) => d.speedup), 1);
  const W = 200;
  const H = 60;
  const PAD = 5;

  const puntos = datos.map((d, i) => {
    const x = PAD + (i / (datos.length - 1)) * (W - PAD * 2);
    const y = PAD + (1 - d.speedup / maxSpeedup) * (H - PAD * 2);
    return `${x},${y}`;
  });

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="w-full h-14">
      <polyline
        points={puntos.join(' ')}
        fill="none"
        stroke="#3b82f6"
        strokeWidth="2"
        strokeLinejoin="round"
        strokeLinecap="round"
      />
    </svg>
  );
}

export default function PanelMetricas({ onNuevaMetricaRef, metricasIniciales }: PanelMetricasProps) {
  const [historial, setHistorial] = useState<NuevaMetricaPayload[]>([]);
  const [ultima, setUltima] = useState<NuevaMetricaPayload | null>(null);

  // Poblar con datos iniciales si los hay
  useEffect(() => {
    if (metricasIniciales.length > 0) {
      const iniciales: NuevaMetricaPayload[] = metricasIniciales.slice(-20).map((m) => ({
        speedup:          m.speedup,
        eficiencia:       m.eficiencia,
        throughput:       m.decisionesPorSegundo,
        cuellobotella:    m.porcentajeLock,
        tiempoParaleloMs: m.tiempoParaleloMs,
        tiempoSecuencialMs: m.tiempoSecuencialMs,
        nucleos:          m.nucleos,
      }));
      setHistorial(iniciales);
      setUltima(iniciales[iniciales.length - 1] ?? null);
    }
  }, [metricasIniciales]);

  // Registrar callback de SignalR
  useEffect(() => {
    onNuevaMetricaRef.current = (data: NuevaMetricaPayload) => {
      setUltima(data);
      setHistorial((prev) => [...prev.slice(-19), data]);
    };
  }, [onNuevaMetricaRef]);

  const cuelloBottella = ultima?.cuellobotella ?? 0;
  const cuellobotellaPct = Math.min(Math.round(cuelloBottella), 100);

  return (
    <div className="bg-slate-800 rounded-xl p-6 space-y-4">
      <h2 className="text-slate-200 font-semibold text-lg">Métricas de rendimiento</h2>

      {/* Números principales */}
      <div className="grid grid-cols-3 gap-3 text-center">
        <div>
          <p className="text-slate-500 text-xs uppercase tracking-wide mb-1">Speedup</p>
          <p className="text-5xl font-bold text-blue-400 tabular-nums leading-none">
            {ultima ? ultima.speedup.toFixed(1) : '0.0'}
            <span className="text-2xl">×</span>
          </p>
        </div>
        <div>
          <p className="text-slate-500 text-xs uppercase tracking-wide mb-1">Eficiencia</p>
          <p className="text-3xl font-semibold text-green-400 tabular-nums">
            {ultima ? Math.round(ultima.eficiencia * 100) : 0}%
          </p>
        </div>
        <div>
          <p className="text-slate-500 text-xs uppercase tracking-wide mb-1">Dec/seg</p>
          <p className="text-2xl font-semibold text-slate-200 tabular-nums">
            {ultima ? Math.round(ultima.throughput).toLocaleString() : '0'}
          </p>
        </div>
      </div>

      {/* Barra cuello de botella */}
      <div>
        <div className="flex justify-between text-xs text-slate-400 mb-1">
          <span>Cuello de botella (lock)</span>
          <span className={cuellobotellaPct > 50 ? 'text-red-400' : 'text-yellow-400'}>
            {cuellobotellaPct}%
          </span>
        </div>
        <div className="bg-slate-700 rounded-full h-3 overflow-hidden">
          <div
            className={`h-full rounded-full transition-all duration-300 ${
              cuellobotellaPct > 50 ? 'bg-red-500' : 'bg-yellow-500'
            }`}
            style={{ width: `${cuellobotellaPct}%` }}
          />
        </div>
      </div>

      {/* Mini gráfica SVG de Speedup */}
      <div>
        <p className="text-slate-500 text-xs uppercase tracking-wide mb-1">Speedup (últimos 20 ciclos)</p>
        <div className="bg-slate-900 rounded-lg p-2">
          {historial.length >= 2 ? (
            <SpeedupChart datos={historial} />
          ) : (
            <div className="h-14 flex items-center justify-center text-slate-600 text-xs">
              Esperando datos…
            </div>
          )}
        </div>
      </div>

      {/* Tabla últimas 10 métricas */}
      <div className="overflow-x-auto">
        <table className="w-full text-sm text-slate-300">
          <thead>
            <tr className="text-slate-500 uppercase text-xs border-b border-slate-700">
              <th className="text-left pb-1">Núcleos</th>
              <th className="text-right pb-1">T.Seq(ms)</th>
              <th className="text-right pb-1">T.Par(ms)</th>
              <th className="text-right pb-1">Speedup</th>
              <th className="text-right pb-1">Efic.</th>
            </tr>
          </thead>
          <tbody>
            {historial.length === 0 ? (
              <tr>
                <td colSpan={5} className="text-center text-slate-600 py-2 text-xs">
                  Sin datos aún
                </td>
              </tr>
            ) : (
              [...historial].reverse().slice(0, 10).map((m, i) => (
                <tr key={i} className="border-b border-slate-700/50">
                  <td className="py-1">{m.nucleos}</td>
                  <td className="text-right tabular-nums">{m.tiempoSecuencialMs}</td>
                  <td className="text-right tabular-nums">{m.tiempoParaleloMs}</td>
                  <td className="text-right tabular-nums text-blue-400">{m.speedup.toFixed(2)}×</td>
                  <td className="text-right tabular-nums text-green-400">
                    {Math.round(m.eficiencia * 100)}%
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
