import { useState, useEffect } from 'react';

const INTERVALOS = [1, 2, 5, 10];

interface PanelConfiguracionProps {
  conectado: boolean;
  nucleosDisponibles: number;
  simulacionActiva: boolean;
  onIniciar: (nucleos: number, intervalo: number) => void;
  onPausar: () => void;
  onConfigurar: (nucleos: number, intervalo: number) => void;
}

export default function PanelConfiguracion({
  conectado,
  nucleosDisponibles,
  simulacionActiva,
  onIniciar,
  onPausar,
  onConfigurar,
}: PanelConfiguracionProps) {
  const [nucleos, setNucleos] = useState(1);
  const [intervalo, setIntervalo] = useState(2);

  // Si la simulación está activa y el usuario cambia configuración → aplicar en caliente
  useEffect(() => {
    if (simulacionActiva) {
      onConfigurar(nucleos, intervalo);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nucleos, intervalo]);

  return (
    <div className="bg-slate-800 rounded-xl p-6 space-y-5">
      {/* Encabezado + estado conexión */}
      <div className="flex items-center justify-between">
        <h2 className="text-slate-200 font-semibold text-lg">Configuración</h2>
        <div className="flex items-center gap-2">
          <div
            className={`w-2 h-2 rounded-full ${conectado ? 'bg-green-400' : 'bg-red-400'}`}
          />
          <span className="text-slate-400 text-xs">
            {conectado ? 'Conectado' : 'Sin conexión'}
          </span>
        </div>
      </div>

      {/* Slider de núcleos */}
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
          onChange={(e) => setNucleos(Number(e.target.value))}
          className="accent-blue-500 w-full"
        />
        <div className="flex justify-between text-slate-500 text-xs mt-1">
          <span>1</span>
          <span>{Math.max(nucleosDisponibles, 1)}</span>
        </div>
      </div>

      {/* Selector de intervalo */}
      <div>
        <p className="text-slate-400 text-sm mb-2">Intervalo entre ticks</p>
        <div className="flex gap-2">
          {INTERVALOS.map((seg) => (
            <button
              key={seg}
              onClick={() => setIntervalo(seg)}
              className={`flex-1 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                intervalo === seg
                  ? 'bg-blue-600 text-white'
                  : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
              }`}
            >
              {seg}s
            </button>
          ))}
        </div>
      </div>

      {/* Botones de control */}
      <div className="flex flex-col gap-2 pt-1">
        {!simulacionActiva ? (
          <button
            onClick={() => onIniciar(nucleos, intervalo)}
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
        <button
          className="bg-slate-600 hover:bg-slate-500 text-white px-4 py-2 rounded-lg font-medium transition-colors"
        >
          Reiniciar portafolio
        </button>
      </div>

      {/* Info de núcleos disponibles */}
      <p className="text-slate-500 text-xs text-center">
        {nucleosDisponibles} núcleos detectados en el servidor
      </p>
    </div>
  );
}
