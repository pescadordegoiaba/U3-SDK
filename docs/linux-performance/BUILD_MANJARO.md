# Build no Manjaro/Arch

Requisitos esperados:

- Unity 2022.3.62f3 com módulo Linux.
- `python3`, `cmake`, `ninja`, `glslangValidator`, `vulkaninfo`, `glxinfo`.
- Mesa RADV para RX 580 como driver principal.

Comandos:

```bash
tools/validate_linux_environment.sh
tools/build_fidelityfx_linux.sh
tools/run_editmode_tests.sh
tools/run_playmode_tests.sh
tools/build_linux_player.sh
```

Nenhum script instala pacotes automaticamente ou usa `sudo`.
