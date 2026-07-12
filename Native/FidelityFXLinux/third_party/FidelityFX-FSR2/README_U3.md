# AMD FidelityFX FSR 2 vendorizado

- Origem: https://github.com/GPUOpen-Effects/FidelityFX-FSR2
- Tag: `v2.2.1`
- Commit: `1680d1edd5c034f88ebbbb793d8b88f8842cf804`
- Versão: FidelityFX FSR 2.2.1
- Licença: MIT, preservada em `LICENSE.txt`

Arquivos vendorizados: API FSR2 comum, backend Vulkan, shaders GLSL oficiais e headers de permutations SPIR-V. DX12, sample, media e Cauldron foram excluídos.

Os headers em `generated/vk` foram produzidos pelo `FidelityFX_SC.exe` oficial da tag, executado por Wine, com os mesmos argumentos Vulkan 1.1 definidos pelo CMake upstream. A build normal não baixa fontes e não depende de uma branch flutuante.

Modificações locais ficam fora da árvore AMD. `src/ffx_linux_compat.h` fornece somente compatibilidade das funções CRT seguras usadas pelo código upstream quando compilado com GCC.

O CMake limita aos quatro `.cpp` AMD as supressões de warnings produzidos pelo estilo upstream/generated (structs anônimas, inicialização parcial de unions, parâmetros não usados e `wstring_convert` depreciado). O bridge U3 continua compilado com `-Wall -Wextra -Wpedantic` sem essas supressões.
