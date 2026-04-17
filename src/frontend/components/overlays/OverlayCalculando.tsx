import { useEffect, useState } from 'react';

interface OverlayCalculandoProps {
  modo: 'Secuencial' | 'Paralelo';
  visible: boolean;
}

export default function OverlayCalculando({ modo, visible }: OverlayCalculandoProps) {
  const [mostrar, setMostrar] = useState(false);
  const [opacity, setOpacity] = useState(0);

  // Animar entrada / salida
  useEffect(() => {
    if (visible) {
      setMostrar(true);
      const t = setTimeout(() => setOpacity(1), 10);
      return () => clearTimeout(t);
    } else {
      setOpacity(0);
      const t = setTimeout(() => setMostrar(false), 400);
      return () => clearTimeout(t);
    }
  }, [visible]);

  if (!mostrar) return null;

  const esParalelo = modo === 'Paralelo';

  return (
    <div
      className="fixed bottom-5 right-5 z-50 pointer-events-none"
      style={{ opacity, transition: 'opacity 0.35s ease' }}
    >
      <div className="bg-slate-800 border border-slate-600 shadow-2xl rounded-xl px-4 py-3 flex items-start gap-3 max-w-xs">
        {/* Spinner */}
        <div
          className="mt-0.5 h-5 w-5 shrink-0 rounded-full border-2 border-t-transparent animate-spin"
          style={{ borderColor: esParalelo ? '#4ade80' : '#facc15', borderTopColor: 'transparent' }}
        />

        {/* Texto */}
        <div>
          <p className="text-slate-100 text-sm font-semibold leading-tight">
            Calculando estrategias
          </p>
          <p className="text-slate-400 text-xs mt-0.5">
            {esParalelo
              ? '⚡ Paralelo — las 3 a la vez (~10 s)'
              : '⏱ Secuencial — una por una (~30 s)'}
          </p>
          <p className="text-slate-600 text-xs mt-1 leading-tight">
            {esParalelo
              ? 'Agresiva ⟂ Conservadora ⟂ Tendencia'
              : 'Agresiva → Conservadora → Tendencia'}
          </p>
        </div>
      </div>
    </div>
  );
}
