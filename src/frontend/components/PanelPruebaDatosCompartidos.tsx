import { useState } from 'react';
import type { PruebaCargaPortafolioPayload } from '@/types/simulador';

interface PanelPruebaDatosCompartidosProps {
  conectado: boolean;
  nucleosSeleccionados: number;
  pruebaCargaPortafolio: PruebaCargaPortafolioPayload | null;
  onEjecutarPruebaCarga: (operaciones: number, concurrencia: number, trabajoCriticoMs: number) => void;
}

export default function PanelPruebaDatosCompartidos({
  conectado,
  nucleosSeleccionados,
  pruebaCargaPortafolio,
  onEjecutarPruebaCarga,
}: PanelPruebaDatosCompartidosProps) {
  const [operacionesCarga, setOperacionesCarga] = useState(20000);
  const [trabajoCriticoMs, setTrabajoCriticoMs] = useState(1);

  const cargaActiva = pruebaCargaPortafolio?.estado === 'iniciada' || pruebaCargaPortafolio?.estado === 'progreso';
  const cargaCompletada = pruebaCargaPortafolio?.estado === 'completada';
  const progresoCarga = pruebaCargaPortafolio && pruebaCargaPortafolio.operaciones > 0
    ? Math.min(100, Math.round((pruebaCargaPortafolio.completadas / pruebaCargaPortafolio.operaciones) * 100))
    : 0;

  return (
    <div className="bg-slate-800 rounded-xl p-4 space-y-3">
      <div className="flex flex-col md:flex-row md:items-start md:justify-between gap-2">
        <div>
          <h2 className="text-slate-200 font-semibold text-base">Prueba de datos compartidos</h2>
          <p className="text-slate-500 text-xs">
            Sobrecarga el portafolio real para demostrar presión de lock sin corrupción del saldo.
          </p>
        </div>
        {pruebaCargaPortafolio && (
          <span className={`text-xs font-semibold ${
            cargaActiva ? 'text-blue-400' : pruebaCargaPortafolio.consistente ? 'text-green-400' : 'text-red-400'
          }`}>
            {cargaActiva ? 'Bajo carga' : pruebaCargaPortafolio.consistente ? 'Sin corrupción' : 'Revisar saldo'}
          </span>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <label className="text-xs text-slate-500">
          Operaciones
          <input
            type="number"
            min={2}
            max={200000}
            step={1000}
            value={operacionesCarga}
            onChange={(e) => setOperacionesCarga(Number(e.target.value))}
            className="mt-1 w-full bg-slate-900 border border-slate-700 rounded-lg px-2 py-1.5 text-slate-200"
          />
        </label>
        <label className="text-xs text-slate-500">
          Lock ms
          <input
            type="number"
            min={0}
            max={20}
            value={trabajoCriticoMs}
            onChange={(e) => setTrabajoCriticoMs(Number(e.target.value))}
            className="mt-1 w-full bg-slate-900 border border-slate-700 rounded-lg px-2 py-1.5 text-slate-200"
          />
        </label>
        <div className="bg-slate-900 rounded-lg px-3 py-2 text-xs text-slate-500 flex items-center">
          Concurrencia:{' '}
          <span className="text-slate-300 font-semibold ml-1">{nucleosSeleccionados}</span>{' '}
          <span className="ml-1">núcleos seleccionados</span>
        </div>
      </div>

      <button
        onClick={() => onEjecutarPruebaCarga(operacionesCarga, nucleosSeleccionados, trabajoCriticoMs)}
        disabled={!conectado || cargaActiva}
        className="w-full bg-blue-600 hover:bg-blue-500 disabled:opacity-40 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg font-medium transition-colors"
      >
        {cargaActiva ? 'Ejecutando prueba...' : 'Ejecutar prueba de carga'}
      </button>

      {pruebaCargaPortafolio && (
        <div className="bg-slate-900 rounded-lg p-3 space-y-2">
          <div className="flex justify-between text-xs text-slate-500">
            <span>{pruebaCargaPortafolio.completadas}/{pruebaCargaPortafolio.operaciones} operaciones</span>
            <span>{progresoCarga}%</span>
          </div>
          <div className="h-2 bg-slate-800 rounded-full overflow-hidden">
            <div
              className={`h-full ${
                cargaActiva ? 'bg-blue-500' : pruebaCargaPortafolio.consistente ? 'bg-green-500' : 'bg-red-500'
              }`}
              style={{ width: `${progresoCarga}%` }}
            />
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-xs">
            <p className="text-slate-500">
              Ganadas <span className="text-green-400 font-mono">{pruebaCargaPortafolio.ganadas}</span>
            </p>
            <p className="text-slate-500">
              Pérdidas <span className="text-red-400 font-mono">{pruebaCargaPortafolio.perdidas}</span>
            </p>
            <p className="text-slate-500">
              Esperado <span className="text-slate-300 font-mono">${pruebaCargaPortafolio.saldoEsperado.toFixed(2)}</span>
            </p>
            <p className="text-slate-500">
              Obtenido <span className="text-slate-300 font-mono">${pruebaCargaPortafolio.saldoObtenido.toFixed(2)}</span>
            </p>
            <p className="text-slate-500">
              Espera lock <span className="text-slate-300 font-mono">{Math.round(pruebaCargaPortafolio.tiempoEsperaLockMs)}ms</span>
            </p>
            <p className="text-slate-500">
              Presión <span className="text-slate-300 font-mono">{Math.round(pruebaCargaPortafolio.porcentajeLock)}%</span>
            </p>
            <p className="text-slate-500">
              Total <span className="text-slate-300 font-mono">{Math.round(pruebaCargaPortafolio.tiempoTotalMs)}ms</span>
            </p>
            <p className="text-slate-500">
              Locks <span className="text-slate-300 font-mono">{pruebaCargaPortafolio.adquisicionesLock}</span>
            </p>
          </div>
          {cargaCompletada && (
            <p className={`text-xs ${pruebaCargaPortafolio.consistente ? 'text-green-400' : 'text-red-400'}`}>
              {pruebaCargaPortafolio.consistente
                ? 'Saldo validado contra el resultado esperado.'
                : 'El saldo obtenido no coincide con el resultado esperado.'}
            </p>
          )}
        </div>
      )}
    </div>
  );
}
