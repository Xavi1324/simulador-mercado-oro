import { useEffect } from 'react';

const INTERVALOS = [1, 2, 5, 10];

interface PanelConfiguracionProps {
  conectado: boolean;
  nucleosDisponibles: number;
  simulacionActiva: boolean;
  isCalculando: boolean;
  saldoPortafolio: number;
  saldoInicialPortafolio: number;
  modoFuente: 'Swissquote' | 'API' | 'CSV' | null;
  nucleos: number;
  intervalo: number;
  onNucleosChange: (nucleos: number) => void;
  onIntervaloChange: (intervalo: number) => void;
  onIniciar: () => void;
  onPausar: () => void;
  onConfigurar: (nucleos: number, intervalo: number) => void;
  onCambiarFuente: (fuente: 'Swissquote' | 'CSV') => void;
}

export default function PanelConfiguracion({
  conectado,
  nucleosDisponibles,
  simulacionActiva,
  isCalculando,
  saldoPortafolio,
  saldoInicialPortafolio,
  modoFuente,
  nucleos,
  intervalo,
  onNucleosChange,
  onIntervaloChange,
  onIniciar,
  onPausar,
  onConfigurar,
  onCambiarFuente,
}: PanelConfiguracionProps) {
  useEffect(() => {
    if (simulacionActiva) {
      onConfigurar(nucleos, intervalo);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nucleos, intervalo]);

  return (
    <div className="bg-slate-800 rounded-xl p-4 space-y-3">
      {/* Encabezado */}
      <div className="flex items-center justify-between">
        <h2 className="text-slate-200 font-semibold text-base">Configuración</h2>
        <div className="flex items-center gap-2">
          <div className={`w-2 h-2 rounded-full ${conectado ? 'bg-green-400' : 'bg-red-400'}`} />
          <span className="text-slate-400 text-xs">{conectado ? 'Conectado' : 'Sin conexión'}</span>
        </div>
      </div>

      {/* Slider núcleos */}
      <div>
        <div className="flex justify-between mb-1">
          <label className="text-slate-400 text-sm">Núcleos de CPU</label>
          <span className="text-slate-200 font-semibold text-sm">{nucleos}</span>
        </div>
        <input
          type="range"
          min={1}
          max={Math.max(nucleosDisponibles, 1)}
          value={nucleos}
          onChange={(e) => onNucleosChange(Number(e.target.value))}
          className="accent-blue-500 w-full"
        />
        <div className="flex justify-between text-slate-500 text-xs mt-1">
          <span>1</span>
          <span>{Math.max(nucleosDisponibles, 1)}</span>
        </div>
      </div>

      {/* Intervalo */}
      <div>
        <p className="text-slate-400 text-sm mb-2">Intervalo entre ticks</p>
        <div className="flex gap-2">
          {INTERVALOS.map((seg) => (
            <button
              key={seg}
              onClick={() => onIntervaloChange(seg)}
              className={`flex-1 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                intervalo === seg ? 'bg-blue-600 text-white' : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
              }`}
            >
              {seg}s
            </button>
          ))}
        </div>
      </div>

      {/* Fuente de datos */}
      <div>
        <p className="text-slate-400 text-sm mb-2">Fuente de datos</p>
        <div className="flex gap-2">
          {(['Swissquote', 'CSV'] as const).map((f) => {
            const activo = modoFuente === f || (f === 'Swissquote' && (modoFuente === 'API' || modoFuente === null));
            return (
              <button
                key={f}
                onClick={() => onCambiarFuente(f)}
                disabled={!conectado}
                className={`flex-1 py-1.5 rounded-lg text-sm font-medium transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
                  activo
                    ? f === 'CSV'
                      ? 'bg-yellow-700 text-yellow-100'
                      : 'bg-blue-700 text-blue-100'
                    : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
                }`}
              >
                {f === 'CSV' ? '📂 CSV Histórico' : '🌐 Swissquote'}
              </button>
            );
          })}
        </div>
      </div>

      {/* Iniciar / Pausar */}
      <div className="flex flex-col gap-2 pt-1">
        {!simulacionActiva ? (
          <button
            onClick={onIniciar}
            disabled={!conectado}
            className="bg-green-600 hover:bg-green-500 disabled:opacity-40 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg font-medium transition-colors"
          >
            Iniciar simulación
          </button>
        ) : (
          <button
            onClick={onPausar}
            disabled={!conectado}
            className="bg-yellow-600 hover:bg-yellow-500 disabled:opacity-40 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg font-medium transition-colors"
          >
            Pausar simulación
          </button>
        )}
      </div>

      {/* ── Portafolio real ───────────────────────────────────────────────── */}
      <div className="border-t border-slate-700 pt-3 space-y-2">
        <p className="text-slate-400 text-xs uppercase tracking-wider font-semibold">
          Portafolio real
        </p>

        <p className="text-slate-500 text-xs text-center">
          {isCalculando
            ? nucleos > 1
              ? 'Calculando estrategias repartidas entre CPU...'
              : 'Calculando estrategias en secuencial...'
            : nucleos > 1
            ? 'Más de 1 CPU → trabajo paralelo'
            : '1 CPU → trabajo secuencial'}
        </p>

        <div className="flex items-center justify-between bg-slate-900 rounded-lg px-3 py-2">
          <span className="text-slate-400 text-xs">Saldo actual</span>
          <span
            className={`text-sm font-bold font-mono ${
              saldoPortafolio >= saldoInicialPortafolio ? 'text-green-400' : 'text-red-400'
            }`}
          >
            ${saldoPortafolio.toFixed(2)}
          </span>
        </div>
      </div>

      <p className="text-slate-500 text-xs text-center">
        {nucleosDisponibles} núcleos detectados en el servidor
      </p>
    </div>
  );
}
