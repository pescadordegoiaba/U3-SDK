# Perfil de Memória 4 GB

Perfis adicionados:

- Muito baixo: alvo aproximado 3,0 GB RSS, texture streaming 256 MB.
- Baixo: alvo aproximado 3,3 GB RSS, texture streaming 384 MB.
- Equilibrado: orçamento maior, texture streaming 512 MB.
- Automático: escolhe baixo em sistemas com menos memória.

O sistema não chama GC completo nem `Resources.UnloadUnusedAssets()` por quadro.
