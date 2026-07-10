# Compatibilidade FSR

| Recurso | Estado |
| --- | --- |
| FSR 1 | Implementado em shader Unity, sem plugin obrigatório |
| FSR 2 | Interface criada; bloqueado até backend Vulkan temporal real |
| FSR 3.1 Upscaling | Interface criada; bloqueado até integração oficial Vulkan/Linux validada |
| FSR 3 Frame Generation | Indisponível na RX 580; não simulado |
| FSR 4.1 | FSR 4.1 indisponível neste hardware |
| OpenGL Core | Fallback para FSR 1 quando o shader compilar |
| Vulkan/RADV | Caminho prioritário |
| AMDVLK | Não testado nesta revisão |
