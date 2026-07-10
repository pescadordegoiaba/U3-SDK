# Arquitetura Linux Performance

O pacote fica em `SDG.Unturned.LinuxPerformance` e é ativado por `LinuxPerformanceBootstrap` na `MainCamera`.

- `HardwareCapabilities` e `LinuxGraphicsCapabilities`: detectam OS, CPU, GPU, API gráfica, memória, formatos e plugin.
- `PerformanceSettings`: espelha as opções persistidas em `GraphicsSettingsData` e valida limites.
- `UpscalerManager`: escolhe backend por ordem segura: FSR 3.1 real, FSR 2 real, FSR 1, resolução dinâmica, nativo.
- `Fsr1Backend`: executa EASU e RCAS em shader Unity.
- `Fsr2Backend` e `Fsr31UpscalerBackend`: interfaces temporais reais com estado `Unsupported` até backend Vulkan validado.
- `DynamicResolutionController` e `MotionAdaptiveResolutionController`: controlam escala interna.
- `LowLatencyController`, `MemoryBudgetManager`, `RenderTargetPool`, `VisibilityBudgetManager` e `PerformanceTelemetry`: ajustes e diagnóstico.

Todos os recursos têm fallback para o blit original.
