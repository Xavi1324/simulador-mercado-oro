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
  const H = 50;
  const PAD = 4;

  const puntos = datos.map((d, i) => {
    const x = PAD + (i / (datos.length - 1)) * (W - PAD * 2);
    const y = PAD + (1 - d.speedup / maxSpeedup) * (H - PAD * 2);
    return `${x},${y}`;
  });

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="w-full h-12">
      <polyline
        points={puntos.join(' ')}
        fill="none"
        stroke="#60a5fa"
        strokeWidth="2"
        strokeLinejoin="round"
        strokeLinecap="round"
      />
    </svg>
  );
}

function etiquetaEficiencia(eficiencia: number): { texto: string; color: string } {
  if (eficiencia >= 0.8) return { texto: 'Excelente', color: 'text-green-400' };
  if (eficiencia >= 0.5) return { texto: 'Buena',     color: 'text-yellow-400' };
  return                        { texto: 'Mejorable', color: 'text-orange-400' };
}

function etiquetaLock(pct: number): { texto: string; color: string } {
  if (pct < 15) return { texto: 'Sin presión',              color: 'text-green-400' };
  if (pct < 40) return { texto: 'Presión moderada',         color: 'text-yellow-400' };
  return               { texto: 'Cuello de botella fuerte', color: 'text-red-400' };
}

export default function PanelMetricas({ onNuevaMetricaRef, metricasIniciales }: PanelMetricasProps) {
  const [historial, setHistorial] = useState<NuevaMetricaPayload[]>([]);
  const [ultima, setUltima] = useState<NuevaMetricaPayload | null>(null);

  useEffect(() => {
    if (metricasIniciales.length > 0) {
      const iniciales: NuevaMetricaPayload[] = metricasIniciales.slice(-20).map((m) => ({
        speedup:           m.speedup,
        eficiencia:        m.eficiencia,
        throughput:        m.decisionesPorSegundo,
        cuellobotella:     m.porcentajeLock,
        tiempoParaleloMs:  m.tiempoParaleloMs,
        tiempoSecuencialMs: m.tiempoSecuencialMs,
        nucleos:           m.nucleos,
      }));
      setHistorial(iniciales);
      setUltima(iniciales[iniciales.length - 1] ?? null);
    }
  }, [metricasIniciales]);

  useEffect(() => {
    onNuevaMetricaRef.current = (data: NuevaMetricaPayload) => {
      setUltima(data);
      setHistorial((prev) => [...prev.slice(-19), data]);
    };
  }, [onNuevaMetricaRef]);

  const speedup  = ultima?.speedup ?? 0;
  const nucleos  = ultima?.nucleos ?? 1;
  const lockPct  = Math.min(Math.round(ultima?.cuellobotella ?? 0), 100);
  const efic     = ultima?.eficiencia ?? 0;
  const eficLabel = etiquetaEficiencia(efic);
  const lockLabel = etiquetaLock(lockPct);

  // Frase resumen en lenguaje natural
  const fraseSpeedup = speedup >= 1.2
    ? `El paralelo terminó ${speedup.toFixed(1)}× más rápido`
    : speedup > 0
    ? 'Sin diferencia notable aún'
    : null;

  const fraseNucleos = nucleos > 1
    ? `${nucleos} agentes en paralelo`
    : '1 agente (secuencial)';

  return (
    <div className="bg-slate-800 rounded-xl p-4 space-y-3">
      <h2 className="text-slate-200 font-semibold text-base">Rendimiento</h2>

      {/* Frase principal */}
      {fraseSpeedup ? (
        <div className="bg-slate-900 rounded-lg px-4 py-3">
          <p className="text-blue-300 font-semibold text-base leading-snug">{fraseSpeedup}</p>
          <p className="text-slate-500 text-xs mt-0.5">{fraseNucleos}</p>
        </div>
      ) : (
        <div className="bg-slate-900 rounded-lg px-4 py-3 text-center text-slate-600 text-sm">
          Esperando primer ciclo…
        </div>
      )}

      {/* Eficiencia + Lock en fila */}
      <div className="grid grid-cols-2 gap-3">
        <div className="bg-slate-900 rounded-lg px-3 py-2.5">
          <p className="text-slate-500 text-xs mb-1">Eficiencia de núcleos</p>
          <p className={`font-bold text-sm ${eficLabel.color}`}>{eficLabel.texto}</p>
          <p className="text-slate-600 text-xs">{Math.round(efic * 100)}% utilizado</p>
        </div>
        <div className="bg-slate-900 rounded-lg px-3 py-2.5">
          <p className="text-slate-500 text-xs mb-1">Portafolio compartido</p>
          <p className={`font-bold text-sm ${lockLabel.color}`}>{lockLabel.texto}</p>
          <p className="text-slate-600 text-xs">{lockPct}% tiempo en espera</p>
        </div>
      </div>

      {/* Gráfica de speedup */}
      {historial.length >= 2 && (
        <div>
          <p className="text-slate-500 text-xs mb-1">Speedup a lo largo del tiempo</p>
          <div className="bg-slate-900 rounded-lg p-2">
            <SpeedupChart datos={historial} />
          </div>
          <div className="flex justify-between text-slate-600 text-xs mt-0.5">
            <span>más lento</span>
            <span>{historial.length} ciclos</span>
            <span>más rápido</span>
          </div>
        </div>
      )}

      {/* Últimos ciclos */}
      {historial.length > 0 && (
        <div>
          <p className="text-slate-500 text-xs mb-1">Últimos ciclos</p>
          <div className="bg-slate-900 rounded-lg overflow-hidden">
            <table className="w-full text-xs">
              <thead>
                <tr className="text-slate-600 uppercase">
                  <th className="px-2 py-1.5 text-left">Núcleos</th>
                  <th className="px-2 py-1.5 text-right">T.Seq</th>
                  <th className="px-2 py-1.5 text-right">T.Par</th>
                  <th className="px-2 py-1.5 text-right">Speedup</th>
                  <th className="px-2 py-1.5 text-right">Efic.</th>
                </tr>
              </thead>
              <tbody>
                {[...historial].reverse().slice(0, 8).map((m, i) => (
                  <tr key={i} className="border-t border-slate-800">
                    <td className="px-2 py-1 text-slate-300">{m.nucleos}</td>
                    <td className="px-2 py-1 text-slate-300 text-right">{Math.round(m.tiempoSecuencialMs)}ms</td>
                    <td className="px-2 py-1 text-slate-300 text-right">{Math.round(m.tiempoParaleloMs)}ms</td>
                    <td className="px-2 py-1 text-blue-400 text-right font-semibold">{m.speedup.toFixed(2)}×</td>
                    <td className="px-2 py-1 text-right">
                      <span className={
                        m.eficiencia >= 0.8 ? 'text-green-400'
                        : m.eficiencia >= 0.5 ? 'text-yellow-400'
                        : 'text-orange-400'
                      }>
                        {Math.round(m.eficiencia * 100)}%
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

    </div>
  );
}
