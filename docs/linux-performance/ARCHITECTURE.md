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

## Culling visual regional

`VisibilityBudgetManager` recebe cada `LevelObject` depois que sua lista de renderers foi preenchida e mantém índices por região. Não há busca global de objetos. O `Tick` considera somente a região atual, as oito vizinhas, regiões entrando/saindo do raio e filas persistentes de mudanças.

Objetos interativos, rubble/destrutíveis, NPCs, objetos de missão ou rede, triggers e assets com `isCollisionImportant` são classificados como `NeverCull`. Colliders, triggers, nav e o estado ativo do `GameObject` não são modificados pelo manager. As únicas mudanças adicionais são `shadowCastingMode`, `Light`, `ParticleSystem` e `Animator` cosmético; a visibilidade principal continua sob autoridade de `LevelObject.SetIsVisibleByGameplayBudget`.

O budget inicial é de 32 transições de visibilidade por `LevelObject`, 16 mudanças de sombra e 8 mudanças para cada grupo de luz, partícula e animator. O limite de 32 não representa renderers individuais: um `LevelObject` pode controlar vários renderers de forma atômica. Itens enfileirados usam generation para invalidar mudanças obsoletas quando o estado desejado se inverte.

Selecionar `Original`, entrar em modo cinematográfico, trocar mapa ou destruir o controlador restaura os estados capturados. A restauração é idempotente e respeita a composição de visibilidade já existente em `LevelObject`, incluindo região, condições e `CullingVolume`.

## Renderização explícita em baixa resolução

`LowResolutionWorldRenderer` existe somente na `MainCamera`. Quando a escala é menor que 1,0 ele configura buffers persistentes separados para `SceneColorLowRes` e `SceneDepthLowRes`, renderiza o mundo nessas dimensões e fornece um `UpscaledColor` no tamanho real do backbuffer. `GLRenderer` executa o backend/fallback nesse target nativo, desenha seus eventos GL depois do upscale e finalmente apresenta no backbuffer. UI `ScreenSpaceOverlay`, mira, textos e `OnGUI` continuam sendo compostos posteriormente pela Unity em resolução nativa.

Minimapa, reflexões e câmeras secundárias não recebem o componente nem passam pelo fluxo. Os targets só são recriados quando resolução, escala, HDR/formato ou descriptor mudam. Escala 1,0 mantém o caminho nativo; falha de criação ou divergência nas dimensões reais do source usa blit direto ao backbuffer.

## Entradas temporais

Quando um backend temporal está realmente ativo, `TemporalCameraController` aplica jitter Halton e publica `TemporalFrameContext` depois que o mundo foi renderizado. O contexto referencia o color/source real, `SceneDepthLowRes`, uma cópia persistente de `_CameraMotionVectorsTexture`, reactive mask neutra, output nativo, matrizes atual/anterior, dimensões verificadas pelas próprias texturas, near/far, FOV, HDR e reversed-Z.

Há debug views para depth linearizado, motion vectors RGB/magnitude, jitter e reactive mask. Motion vectors continuam explicitamente **não validados visualmente**: câmera, jogador, arma, animator/skinning, veículo, eixo Y e Vulkan precisam de inspeção no Player gráfico. A reactive mask neutra é funcional como fallback, porém incompleta para transparências, água, fogo e partículas; composition mask permanece nula.

O histórico é invalidado por troca/ativação de backend, câmera, mapa, teleporte, morte/respawn, resize, mudança de escala ou FOV e frame anormal. Esses resets não habilitam FSR2 por si mesmos; `has_fsr2` continua zero enquanto o dispatch nativo não existir e não for validado.

## Bridge Vulkan

O plugin registra os callbacks oficiais `UnityPluginLoad`/`UnityPluginUnload`, acompanha os eventos de device e obtém imagens exclusivamente por `IUnityGraphicsVulkan::AccessTexture`. O evento recebe uma struct versionada em ring unmanaged persistente, com slot e generation validados. Source e output entram em `VK_IMAGE_LAYOUT_GENERAL` por barriers geradas pela interface Unity, são usados no command buffer corrente e voltam ao controle da Unity após o callback.

O compute smoke lê uma textura RGBA8, inverte o canal vermelho e escreve outra textura. Pipeline, descriptor pool, descriptor sets e shader module são criados fora do caminho por frame; image views são reutilizadas por slot e só mudam quando a imagem Unity real muda. O readback PlayMode valida duas dimensões. `has_vulkan_backend` só passa a 1 depois desse readback; `has_fsr2`, FSR 3.1 e Frame Generation continuam zero.
