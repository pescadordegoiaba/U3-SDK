# Solução de Problemas

- Se FSR 1 não aparecer, confirme que `Assets/Resources/Shaders/LinuxPerformance/FSR1.shader` foi incluído no build.
- Se FSR 2/3.1 aparecerem indisponíveis, verifique `UpscalerManager.LastSelectionReason`.
- Se o plugin não carregar, execute `tools/build_fidelityfx_linux.sh` e confirme `Assets/Plugins/x86_64/libFidelityFXLinux.so`.
- Se houver stutter inicial, a compilação de shaders no primeiro build pode levar 30-40 minutos.
