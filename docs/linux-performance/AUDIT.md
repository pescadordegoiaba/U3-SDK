# Auditoria Linux Performance

## Arquitetura encontrada

- Unity: 2022.3.62f3.
- Pipeline: Built-in Render Pipeline, sem URP/HDRP.
- Pós-processamento: `com.unity.postprocessing` 3.4.0, com efeitos customizados `SkyFog` e `SrScope`.
- Cena inicial: `Assets/GameStartup.unity`.
- Cenas principais: `Assets/Game/Sources/Scenes/Menu.unity`, `Game.unity`, `Loading.unity`.
- Câmera principal runtime: `MainCamera`, que registra `UnturnedPostProcess` e `GLRenderer`.
- Configurações gráficas: `GraphicsSettingsData`, `GraphicsSettings`, `MenuConfigurationGraphicsUI`.
- Persistência: `/Settings/Graphics.json` via `ReadWrite.serializeJSON`.
- Render path padrão: deferred/forward configurável pelo jogo; `GraphicsSettings.apply` aplica path, HDR, MSAA off, far clip, layer culling e LOD bias.
- Motion vectors: shader de motion vectors existe em `ProjectSettings/GraphicsSettings.asset`; câmeras de cena estavam com dynamic resolution desligado.
- Plugins nativos existentes: somente Json.NET em `Assets/Plugins`.
- Testes existentes: EditMode em `Assets/Editor/Assembly-CSharp-Editor/Tests` e pacotes em `Assets/Tests`.

## Linha de base local

- CPU: Intel Core i7-3770, 4 núcleos/8 threads.
- GPU: AMD Radeon RX 580 Series, Polaris10, 4 GB de VRAM.
- Driver Vulkan: Mesa RADV.
- OpenGL: Mesa radeonsi, OpenGL 4.6.
- RAM: sistema com 15 GiB total; no momento da auditoria havia cerca de 7,3 GiB disponível.

## Arquivos modificados/integrados

- `GraphicsSettingsData.cs` e `GraphicsSettings.cs`: persistência, validação e aplicação das novas opções Linux.
- `MainCamera.cs`: inicialização do bootstrap Linux.
- `GLRenderer.cs`: hook de upscaler com fallback para `Graphics.Blit`.
- `MenuConfigurationGraphicsUI.cs`: controles de upscaler, resolução, memória, culling e overlay.
- `Assets/Runtime/Assembly-CSharp/Unturned/LinuxPerformance/`: arquitetura runtime.
- `Assets/Resources/Shaders/LinuxPerformance/FSR1.shader`: shader FSR 1.
- `Native/FidelityFXLinux/`: plugin ABI Linux.
- `tools/`: scripts Linux.

## Pontos de integração

- O caminho original continua sendo `Graphics.Blit(source, destination)`.
- O upscaler só intercepta o frame quando há backend disponível e opção habilitada.
- FSR 2/3.1 dependem de plugin temporal Vulkan real; quando indisponível retornam motivo legível e caem para FSR 1/resolução nativa.

## Limitações e riscos

- Built-in Pipeline limita acesso a recursos Vulkan internos necessários para FSR 2/3.1 temporal completo.
- FSR 1 é espacial e não usa histórico temporal.
- Resolução dinâmica depende do comportamento da Unity com `ScalableBufferManager` no Built-in Pipeline.
- Benchmarks de gameplay precisam de cenário automatizado para resultados comparáveis.

## Recursos possíveis

- FSR 1 espacial com EASU/RCAS.
- Resolução dinâmica e adaptativa por movimento.
- Perfil de memória com texture streaming.
- Overlay de diagnóstico.
- Plugin `.so` com ABI versionada e fallback.

## Recursos impossíveis ou bloqueados nesta revisão

- FSR 4.1 na RX 580: indisponível pelo hardware e pelo SDK oficial atual.
- Frame Generation na RX 580: indisponível; não será simulado.
- FSR 2/3.1 funcional: bloqueado até integração Vulkan temporal validada com color, depth, motion vectors, jitter e recursos de apresentação.
