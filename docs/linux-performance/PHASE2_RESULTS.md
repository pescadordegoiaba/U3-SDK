# Resultados da fase 2

Data: 2026-07-10.

## Comprovadamente funcional

- Build C# scripts-only Linux concluída com sucesso.
- Build Linux completa concluída com sucesso.
- Build nativa `libFidelityFXLinux.so` concluída com CMake/Ninja.
- Shader `Hidden/Unturned/LinuxPerformance/FSR1` compilou para Vulkan com passes `EASU` e `RCAS`.
- Player gráfico iniciou com `-force-vulkan` e detectou `AMD Radeon RX 580 Series (RADV POLARIS10)`.
- Plugin `.so` foi pré-carregado pelo Player.

## Compilado, mas não validado visualmente

- FSR 1 EASU + RCAS.
- Overlay de diagnóstico.
- Overrides de linha de comando:
  - `-LinuxPerformanceForceFsr1`
  - `-LinuxPerformanceForceNative`
  - `-LinuxPerformanceDebugOverlay`
  - `-LinuxPerformanceCapture`

## Testado automaticamente

O Test Runner foi executado, mas não produziu XML. Portanto não há testes aprovados/reprovados verificáveis.

Comandos:

```bash
tools/run_editmode_tests.sh
tools/run_playmode_tests.sh
```

Resultados observados:

- EditMode: Unity retornou 0, mas `TestResults/editmode-linux-performance.xml` não foi gerado.
- PlayMode: Unity retornou 0, mas `TestResults/playmode-linux-performance.xml` não foi gerado.
- Execução paralela EditMode/PlayMode abortou uma instância com código 134 porque a Unity não permite duas instâncias no mesmo projeto.

Evidência dos logs:

- `TestRunnerInitializer` aparece no log.
- `TestRunnerApiListener` aparece no log.
- `Batchmode quit successfully invoked` aparece sem `RunStarted`/`RunFinished`.

## Testado no Player gráfico

Comando:

```bash
timeout 45s Builds/Linux64/Unturned.x86_64 -force-vulkan -screen-width 1280 -screen-height 1024 -LinuxPerformanceForceFsr1 -LinuxPerformanceDebugOverlay -LinuxPerformanceCapture -logFile Logs/linux-performance/player-vulkan-fsr1.log
```

Resultado:

- Código 124 por timeout.
- O Player inicializou Vulkan e RX 580.
- O log não mostrou exceção fatal.
- O log não registrou `Linux performance capabilities`, então o bootstrap da `MainCamera` não foi comprovadamente alcançado antes do timeout.
- Nenhuma captura foi gerada em `Builds/Linux64/Benchmark/Captures/`.

## Testado na RX 580

Detectado pelo ambiente:

- GPU: `AMD Radeon RX 580 Series (RADV POLARIS10)`.
- Driver Vulkan: Mesa RADV.
- Vulkan API reportada pelo driver: 1.4.348.
- Unity solicitou Vulkan 1.1.0.

Medições de FPS, percentis e frametime não foram coletadas.

## Benchmark

Comando:

```bash
tools/benchmark_linux.sh
```

Resultado:

- JSON: `BenchmarkResults/linux-benchmark-20260710T221133Z.json`
- CSV: `BenchmarkResults/linux-benchmark-20260710T221133Z.csv`
- Status registrado: ferramenta criada, benchmark de gameplay não executado por ausência de cenário automatizado.

## Memória

Sem medição sustentada em gameplay.

Dados observados:

- Ambiente no momento da validação: 15 GiB total, 5.9 GiB disponível.
- Log do Player após timeout mostrou alocadores Unity com picos internos, mas isso não substitui medição RSS sustentada.

## Culling

Correção aplicada:

- `VisibilityBudgetManager` preserva distâncias originais por câmera e evita redução cumulativa ao reaplicar preset.

Não validado:

- redução real de draw calls;
- gameplay culling;
- occlusion baked;
- HZB.

## Unsupported

- FSR 3 Frame Generation na RX 580.
- FSR 4.1 na RX 580.
- VRS, ray tracing, mesh shaders e recursos de IA/matriz.

## Bloqueado

- FSR 2 temporal real: plugin atual não acessa recursos Vulkan da Unity nem integra SDK temporal oficial.
- FSR 3.1 Upscaling real: mesmo bloqueio do FSR 2.
- Validação visual comparativa automatizada: Player não chegou ao bootstrap/captura dentro do timeout executado.
- Test Runner XML: CLI carrega o pacote, mas encerra sem iniciar execução e sem salvar XML.
