interface OverlayCalculandoProps {
  modo: 'Secuencial' | 'Paralelo';
}

export default function OverlayCalculando({ modo }: OverlayCalculandoProps) {
  const tiempoEsperado = modo === 'Secuencial' ? '~30 s (3 × 10 s secuenciales)' : '~10 s (Task.WhenAll paralelo)';

  return (
    <div className="absolute inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 rounded-xl">
      <div className="text-center px-8">
        {/* Spinner */}
        <div className="h-16 w-16 border-4 border-blue-500 border-t-transparent rounded-full mx-auto mb-5 animate-spin" />

        {/* Texto principal */}
        <p className="text-2xl font-mono font-semibold text-white mb-1">
          Calculando estrategias
        </p>
        <p className="text-base text-slate-300 mb-4">
          Modo:{' '}
          <span className={modo === 'Paralelo' ? 'text-green-400 font-bold' : 'text-yellow-400 font-bold'}>
            {modo}
          </span>
        </p>

        {/* Tiempo esperado */}
        <div className="bg-slate-800/80 rounded-lg px-5 py-3 inline-block">
          <p className="text-sm text-slate-400">Tiempo esperado</p>
          <p className="text-lg font-mono text-slate-100 mt-0.5">{tiempoEsperado}</p>
        </div>

        {/* Explicación pedagógica */}
        {modo === 'Secuencial' ? (
          <p className="mt-4 text-xs text-slate-500 max-w-xs">
            Agresiva → Conservadora → Tendencia<br />
            Cada estrategia espera a que termine la anterior
          </p>
        ) : (
          <p className="mt-4 text-xs text-slate-500 max-w-xs">
            Agresiva ⟂ Conservadora ⟂ Tendencia<br />
            Las 3 estrategias corren en paralelo con Task.WhenAll
          </p>
        )}
      </div>
    </div>
  );
}
