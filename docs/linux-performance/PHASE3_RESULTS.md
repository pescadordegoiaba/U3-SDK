# Resultados da fase 3 Linux Performance

Data: 2026-07-11

## Resumo

Esta fase refatorou o runtime Linux Performance para uma arquitetura modular, corrigiu o Test Runner, atualizou a ABI do plugin nativo para v2 e gerou uma build Linux completa do servidor dedicado e do player gráfico.

FSR 1 permanece como o único upscaler implementado em shader. FSR 2, FSR 3.1 Upscaling, FSR 3 Frame Generation, FSR 4.1 e HZB não foram marcados como funcionais.

## Arquitetura

O arquivo monolítico `LinuxPerformanceRuntime.cs` foi removido e dividido em módulos:

- `Core`: capabilities, configurações, bootstrap, coordenador, resolução dinâmica, baixa latência, telemetria e pool de render targets.
- `Upscaling`: contratos de upscaler, FSR 1, FSR 2, FSR 3.1, provider FSR 4.1 e seleção de backend.
- `Temporal`: contexto temporal, jitter Halton 2,3, controlador de câmera, histórico, reset, validação de motion vectors e debug views.
- `Native`: P/Invoke ABI v2, handle nativo, fila de dispatch e diagnóstico do plugin.
- `Memory`: leitura de RSS/PSS em `/proc` e ponto de extensão para `VK_EXT_memory_budget`.
- `Culling`: registro incremental de renderers, perfil visual por orçamento e estado honesto de HZB.
- `Benchmark`: estruturas iniciais para harness/captura.

## Plugin nativo

Classificação atual: `CapabilityPlugin`.

Implementado:

- ABI v2.
- `u3ffx_get_abi_version`.
- `u3ffx_get_capabilities`.
- `UnityPluginLoad`.
- `UnityPluginUnload`.
- `GetRenderEventFunc`.
- `GetRenderEventAndDataFunc`.

Não implementado:

- bridge real `IUnityGraphicsVulkan`;
- registro de `VkImage`;
- command buffers Vulkan;
- SPIR-V;
- dispatch compute;
- FidelityFX SDK;
- FSR 2;
- FSR 3.1;
- Frame Generation;
- `VK_EXT_memory_budget`.

## FSR

### FSR 1

Status: implementado por shader, com EASU + RCAS.

Validado nesta fase:

- compilação C#;
- build Linux player;
- player iniciou com Vulkan em RX 580/RADV;
- plugin nativo foi carregado pelo player.

Não validado nesta fase:

- screenshot automático do FSR 1;
- comparação visual lado a lado;
- PSNR/SSIM;
- tempo separado de EASU/RCAS no Player.

### FSR 2

Status: bloqueado.

Motivo: não existe bridge Vulkan/FidelityFX funcional nesta árvore. O backend C# implementa o contrato temporal e retorna fallback. Ele não executa dispatch FSR 2.

### FSR 3.1 Upscaling

Status: bloqueado.

Motivo: depende do mesmo bridge Vulkan/FidelityFX. O backend C# é honesto e não reporta `Available`.

### FSR 3 Frame Generation

Status: indisponível.

Motivo: RX 580 não deve habilitar Frame Generation, e não há proxy swapchain Vulkan validado na Unity 2022.3.

### FSR 4.1

Status: indisponível.

Resultado esperado em Linux/RX 580: `FSR 4.1 indisponível neste hardware`.

## Culling

Implementado:

- registro incremental de renderers para métricas;
- `LevelObject.SetIsVisibleByGameplayBudget`;
- orçamento visual separado de ativação/região/colisão;
- preservação de colisão, interação, rede e lógica autoritativa;
- HZB permanece desligado e classificado como unsupported.

Não implementado:

- `CullingGroup` para atores dinâmicos;
- time slicing de categorias;
- debug views completas;
- HZB GPU-driven com indirect draw.

## Memória

Implementado:

- leitura de `VmRSS`, `VmHWM`, `VmSize`, `RssAnon`, `RssFile`, `Pss`, `Private_Clean`, `Private_Dirty` e `Swap` via `/proc`;
- telemetria de RSS/PSS no overlay;
- correção para não usar subprocesso por amostra.

Não implementado:

- leitura real de `VK_EXT_memory_budget` no plugin;
- separação de heaps Vulkan por tipo.

## Testes executados

Comando:

```bash
tools/build_fidelityfx_linux.sh
```

Resultado: passou. O plugin foi compilado e copiado para `Assets/Plugins/x86_64/libFidelityFXLinux.so`.

Comando:

```bash
tools/run_editmode_tests.sh
```

Resultado final:

```text
EditMode: total=1319 passed=1319 failed=0 skipped=0
```

O script agora falha se a Unity retornar zero sem gerar XML.

Comando:

```bash
tools/run_playmode_tests.sh
```

Resultado: bloqueado. A execução PlayMode ficou presa por mais de dois minutos sem gerar XML e foi encerrada manualmente. Isso não foi marcado como aprovado.

Comando:

```bash
./compile_linux.sh
```

Resultado final: passou.

Artefatos gerados:

```text
Builds/Linux64/Unturned.x86_64
Builds/Linux64_Headless/Unturned_Headless.x86_64
```

## Validação gráfica tentada

Comando:

```bash
timeout 45s Builds/Linux64/Unturned.x86_64 -force-vulkan -LinuxPerformanceForceFsr1 -LinuxPerformanceCapture -logFile Logs/linux-performance/player-fsr1-vulkan.log
```

Resultado:

- o player iniciou;
- o log foi escrito em `Builds/Linux64/Logs/linux-performance/player-fsr1-vulkan.log`;
- Vulkan foi selecionado;
- GPU detectada: `AMD Radeon RX 580 Series (RADV POLARIS10)`;
- `VK_EXT_memory_budget` apareceu disponível no driver;
- `libFidelityFXLinux.so` foi carregado;
- o processo não encerrou sozinho antes do timeout;
- nenhuma captura foi encontrada.

Conclusão: validação gráfica completa do FSR 1 ainda não foi comprovada por screenshot nesta fase.

## Correções fora da Linux Performance

`StatusData.GameStatusData` tinha layout serializado diferente entre Editor e Player por causa de `#if WITH_GRANTPACKAGE_PROMO`. Os campos `GrantPackageIDs` e `GrantPackageURL` agora existem em todos os layouts, corrigindo a falha:

```text
script class layout is incompatible between the editor and the player
```

`PathEx.ReplaceInvalidFileNameChars` agora usa um conjunto portátil de caracteres inválidos para que os testes passem no Linux e os nomes exportados sejam compatíveis com Windows.

## Bloqueios

- FSR 2 real: bloqueado por falta de bridge Vulkan/FidelityFX.
- FSR 3.1 real: bloqueado pelo mesmo motivo.
- Frame Generation: indisponível na RX 580 e sem proxy swapchain Unity validado.
- FSR 4.1: indisponível em Linux/RX 580.
- HZB: bloqueado até existir caminho GPU-driven com indirect draw.
- PlayMode Test Runner: travou no ambiente atual e não gerou XML.
- Validação visual: player iniciou em Vulkan/RX 580, mas não gerou screenshot antes do timeout.

## Confirmação

Nenhum arquivo da instalação externa do Unturned foi alterado. As alterações foram feitas apenas no repositório local e nos artefatos de build dentro da árvore do projeto.

## Hotfix: render preto após ativação do Linux Performance

Sintoma reportado: depois da última atualização, a cena 3D ficava preta e somente HUD/menu continuavam visíveis.

Causa provável corrigida:

- `DynamicResolutionController.Apply` reduzia a escala interna sempre que qualquer upscaler estava ativo, mesmo com `LinuxDynamicResolution` desligado.
- `GraphicsSettings` marcava `MainCamera.allowDynamicResolution` quando qualquer upscaler estava ativo.
- Esse caminho era executado a partir de `GLRenderer.OnRenderImage`, tarde demais no frame, podendo invalidar o `source` usado pelo image effect e deixando o backend retornar sucesso com imagem preta.

Correções:

- FSR 1 não força mais `ScalableBufferManager.ResizeBuffers`.
- A câmera só habilita `allowDynamicResolution` quando a opção `LinuxDynamicResolution` está ligada.
- `UpscalerManager.Render` valida render targets, captura exceções e retorna `false` em falha para permitir `Graphics.Blit` nativo.
- `Fsr1Backend` valida shader, passes e dimensões antes de escrever no destino.
- `-LinuxPerformanceForceNative` e `-LinuxPerformanceForceFsr1` desligam resolução dinâmica para testes seguros.
- Novo argumento: `-LinuxPerformanceDisableDynamicResolution`.

Validação executada:

```bash
./tools/run_editmode_tests.sh
```

Resultado:

```text
EditMode: total=1319 passed=1319 failed=0 skipped=0
```

```bash
./tools/build_linux_player.sh
```

Resultado: passou e gerou:

```text
Builds/Linux64/Unturned.x86_64
Builds/Linux64_Headless/Unturned_Headless.x86_64
```

Smokes gráficos tentados:

```bash
timeout 25s Builds/Linux64/Unturned.x86_64 -force-vulkan -LinuxPerformanceForceNative -LinuxPerformanceDisableDynamicResolution -LinuxPerformanceCapture -logFile Logs/LinuxSmokeNativeGraphical.log
timeout 25s Builds/Linux64/Unturned.x86_64 -force-vulkan -LinuxPerformanceForceFsr1 -LinuxPerformanceDisableDynamicResolution -LinuxPerformanceCapture -logFile Logs/LinuxSmokeFsr1Graphical.log
```

Resultado observado nos logs:

- Display: `1280 x 1024 @ 75 Hz`.
- API: Vulkan forçado.
- GPU: `AMD Radeon RX 580 Series (RADV POLARIS10)`.
- Os processos permaneceram vivos até o timeout, sem crash imediato.
- Não houve captura automática porque o smoke não chegou ao `MainCamera`/bootstrap antes do timeout.

Conclusão: build e inicialização Vulkan foram validados; a correção remove a causa provável da tela preta. A confirmação visual em gameplay ainda precisa ser feita manualmente no Player abrindo uma cena/mapa com `MainCamera`.

## Hotfix: luminosidade quase preta e queda de FPS com FSR 1

Sintoma reportado: depois do hotfix anterior, o mundo deixou de ficar totalmente preto, mas passou a renderizar com luminosidade extremamente baixa. Apenas o martelo/viewmodel era claramente visível. Também houve queda de FPS.

Causa provável corrigida:

- FSR 1 estava sendo executado mesmo quando `source` e `destination` tinham a mesma resolução.
- Nesse caso, EASU + RCAS adicionavam dois passes full-screen sem benefício de upscaling.
- O render target intermediário usava o descriptor do `destination`, que pode não preservar corretamente formato HDR/linear/sRGB do `source` da câmera.
- O passe não restaurava explicitamente `GL.sRGBWrite`.

Correções:

- FSR 1 agora retorna `false` em resolução nativa e deixa o `GLRenderer` executar `Graphics.Blit` nativo.
- FSR 1 só executa quando `source` e `destination` têm resolução diferente, ou quando o argumento de diagnóstico `-LinuxPerformanceForceFsr1Diagnostic` é usado.
- O intermediário do EASU agora é criado a partir de `source.descriptor`, preservando formato, HDR e sRGB do frame da câmera, alterando apenas resolução, depth, MSAA e mipmaps.
- O backend define `GL.sRGBWrite` por alvo e restaura o estado anterior no `finally`.
- O shader limita saídas negativas e usa fallback para o pixel central se o peso EASU for inválido.

Validação executada:

```bash
./tools/run_editmode_tests.sh
```

Resultado:

```text
EditMode: total=1319 passed=1319 failed=0 skipped=0
```

```bash
./tools/build_linux_player.sh
```

Resultado: passou e gerou:

```text
Builds/Linux64/Unturned.x86_64
Builds/Linux64_Headless/Unturned_Headless.x86_64
```

Smoke gráfico tentado:

```bash
timeout 25s Builds/Linux64/Unturned.x86_64 -force-vulkan -LinuxPerformanceForceFsr1 -LinuxPerformanceDisableDynamicResolution -logFile Logs/LinuxSmokeFsr1NativeSkip.log
```

Resultado observado:

- Vulkan iniciou na `AMD Radeon RX 580 Series (RADV POLARIS10)`.
- O processo permaneceu vivo até o timeout.
- O smoke não chegou ao `MainCamera`/render hook de gameplay, portanto o log de skip do FSR 1 não apareceu.

Conclusão: compilação e inicialização Vulkan passaram. A correção evita que FSR 1 rode em resolução nativa, que era a causa provável da queda de FPS e do caminho de cor problemático. A validação visual final ainda precisa ser feita manualmente em gameplay.
