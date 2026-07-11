# Relatório de Implementação

## Implementado

- Auditoria e documentação Linux.
- Persistência de novas opções gráficas.
- Menu gráfico com opções Linux em português.
- FSR 1 em shader com passes EASU e RCAS baseados no algoritmo AMD FidelityFX FSR 1.
- Resolução dinâmica, resolução adaptativa por movimento, baixa latência genérica, perfis de memória, culling básico e overlay.
- Plugin Linux `.so` com ABI versionada, consulta de capacidades e fallback.
- Scripts Linux de validação, build, testes e benchmark.
- Marcadores Unity Profiler para hook de renderização, FSR1 EASU/RCAS, resolução dinâmica, telemetria e culling.
- Overrides de linha de comando para validação: `-LinuxPerformanceForceFsr1`, `-LinuxPerformanceForceNative`, `-LinuxPerformanceDebugOverlay` e `-LinuxPerformanceCapture`.

## Experimental

- Resolução dinâmica no Built-in Pipeline via `ScalableBufferManager`.
- Culling agressivo.
- Captura visual automática no Player; criada, mas não validada porque o Player não alcançou o bootstrap antes do timeout executado.

## Bloqueado

- FSR 2/3.1 temporal real: o plugin atual é apenas `Capability plugin`; faltam `IUnityGraphicsVulkan`, acesso a recursos Vulkan da Unity, command buffers, imagens, sincronização, pipelines e SDK temporal oficial integrado.
- Frame Generation na RX 580.
- FSR 4.1 na RX 580.
- Test Runner XML: Unity 2022.3.62f3 com `com.unity.test-framework@1.4.6` carrega o pacote, mas encerra sem executar/salvar resultados via CLI neste repositório.

## Não testado ainda

- AMDVLK.
- Benchmark de gameplay comparável.
- PSNR/SSIM e comparação visual lado a lado Native/Bilinear/FSR1.
- Medição sustentada de RSS/VRAM durante gameplay.

## Validação executada

- `tools/build_fidelityfx_linux.sh`: passou, gerou `libFidelityFXLinux.so`.
- `./compile_linux.sh --scripts-only`: passou.
- `tools/build_linux_player.sh`: passou, gerou player e servidor Linux.
- Build Linux completa compilou `Hidden/Unturned/LinuxPerformance/FSR1` para Vulkan com passes `EASU` e `RCAS`.
- Player gráfico com `-force-vulkan`: inicializou em `AMD Radeon RX 580 Series (RADV POLARIS10)`, mas terminou por timeout e não gerou captura.
- `tools/run_editmode_tests.sh`: bloqueado porque a Unity encerrou sem gerar XML do Test Runner; o script retorna erro quando isso acontece.
- `tools/run_playmode_tests.sh`: mesmo bloqueio do EditMode.
# Atualização da fase 3 - 2026-07-11

Consulte `docs/linux-performance/PHASE3_RESULTS.md` para os resultados executados desta fase.

Resumo desta fase:

- `LinuxPerformanceRuntime.cs` foi dividido em módulos por responsabilidade.
- O plugin nativo foi atualizado para ABI v2, mas continua classificado como `CapabilityPlugin`.
- O contrato temporal para FSR 2/FSR 3.1 foi criado, sem fingir dispatch funcional.
- `TemporalCameraController` aplica jitter Halton e prepara matrizes, mas o backend nativo ainda não consome recursos Vulkan.
- EditMode passou com `1319` testes.
- A build Linux gerou player e servidor.
- O player iniciou com Vulkan na RX 580/RADV, mas a captura visual automática não foi comprovada.
- FSR 2, FSR 3.1, Frame Generation, FSR 4.1 e HZB permanecem indisponíveis/bloqueados.
