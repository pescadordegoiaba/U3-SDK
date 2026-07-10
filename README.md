# U3 SDK

Source code for [Unturned](https://smartlydressedgames.com/unturned/), a free open-world zombie survival sandbox game.

## Getting Started

1. Download/clone this repository
2. Install [Unity Hub](https://unity.com/download) (required to install engine)
3. Install the [Unity 2022.3.62f3](https://unity.com/releases/editor/whats-new/2022.3.62f3) editor
4. *Optional*: if making code changes, select **Game development with Unity** + **.NET desktop development** in the Visual Studio installer
5. Ensure Steam is running and you have [Unturned](https://store.steampowered.com/app/304930/Unturned/) installed (large binary files and mods are loaded from here)
6. Open the project with the Unity editor
7. Open the `Assets/GameStartup.unity` scene
8. Click play!

## Resources

- [Frequently Asked Questions](https://docs.smartlydressedgames.com/en/stable/u3-sdk/faq.html)
- [Source Code Demo: Adding a Heat-Seeking Missile on YouTube](https://youtu.be/CqJnkcWfmEY)
- [Unturned's Modding Documentation](https://docs.smartlydressedgames.com/en/stable/)

## Otimizações Linux Performance

Este fork adiciona uma infraestrutura experimental de desempenho para Linux, focada em:

- Unity 2022.3.62f3;
- Linux x86_64;
- Manjaro/Arch Linux;
- Vulkan como API principal;
- Mesa RADV em GPUs AMD;
- Radeon RX 580/Polaris, incluindo placas de 4 GB;
- Intel Core i7-3770;
- sistemas com cerca de 4 GB de RAM livre para o jogo.

### O que está funcional

No estado atual, **apenas o FSR 1 está funcional**.

O FSR 1 foi implementado em shader Unity com:

- EASU;
- RCAS;
- presets de qualidade;
- nitidez configurável;
- fallback para renderização nativa;
- funcionamento sem plugin nativo obrigatório.

O shader fica em:

```text
Assets/Resources/Shaders/LinuxPerformance/FSR1.shader
```

### O que ainda falta implementar

Os itens abaixo **não estão funcionais ainda** e permanecem bloqueados ou pendentes:

- FSR 2 temporal real;
- FSR 3.1 Upscaling real;
- FSR 3 Frame Generation;
- FSR 4.1;
- backend Vulkan FidelityFX completo;
- benchmark automatizado de gameplay;
- validação visual automatizada lado a lado;
- culling avançado por gameplay;
- HZB;
- medição completa de RSS/VRAM em gameplay.

O plugin nativo Linux atual **não é um backend FidelityFX completo**. Ele é apenas um plugin de ABI/capacidades usado para validar carregamento e fallback.

### Como compilar no Linux

Na raiz do projeto:

```bash
./compile_linux.sh
```

Também é possível executar os scripts individuais:

```bash
tools/validate_linux_environment.sh
tools/build_fidelityfx_linux.sh
tools/build_linux_player.sh
```

O script informa que há partes grandes de shaders e que a primeira compilação pode demorar. Em máquinas sem cache de shader, a estimativa é de 30 a 40 minutos.

### Como rodar o Player Linux gerado

Depois da build:

```bash
Builds/Linux64/Unturned.x86_64
```

Para forçar Vulkan:

```bash
Builds/Linux64/Unturned.x86_64 -force-vulkan
```

Para testar o FSR 1 diretamente:

```bash
Builds/Linux64/Unturned.x86_64 -force-vulkan -LinuxPerformanceForceFsr1 -LinuxPerformanceDebugOverlay
```

### Scripts disponíveis

```text
tools/build_fidelityfx_linux.sh
tools/validate_linux_environment.sh
tools/run_editmode_tests.sh
tools/run_playmode_tests.sh
tools/build_linux_player.sh
tools/benchmark_linux.sh
```

### Documentação detalhada

A documentação técnica fica em:

```text
docs/linux-performance/
```

Arquivos principais:

- `AUDIT.md`;
- `PHASE2_AUDIT.md`;
- `PHASE2_RESULTS.md`;
- `IMPLEMENTATION_REPORT.md`;
- `FSR_COMPATIBILITY.md`;
- `BUILD_MANJARO.md`;
- `TROUBLESHOOTING.md`.

### Observações importantes

- Nenhum arquivo da instalação externa do Unturned é alterado por esta infraestrutura.
- Os binários gerados ficam em `Builds/` e não devem ser versionados.
- Logs, resultados de teste e benchmark ficam em `Logs/`, `TestResults/` e `BenchmarkResults/`.
- Recursos não suportados devem cair para FSR 1 ou renderização nativa, sem tela preta.
