# Ajustes i7-3770

- Evitar alocações por quadro nos caminhos novos.
- Reutilizar render targets via pool.
- Manter trabalho pesado fora de jobs minúsculos.
- Não exigir AVX2/FMA.
- Usar `Profiler`/telemetria para medir antes de alterar algoritmos existentes.
