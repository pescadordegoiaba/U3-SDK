# Licenças do Plugin Linux FidelityFX

Este diretório contém a ponte ABI local, o compute smoke Vulkan do U3-SDK e o SDK oficial AMD FidelityFX FSR 2.2.1 vendorizado. O smoke isolado usa somente headers oficiais da Unity, Vulkan e um shader GLSL próprio do projeto.

FSR 1 é implementado no shader Unity do projeto. Código oficial AMD usado como referência deve manter o aviso MIT da AMD quando incorporado.

FSR 2.2.1 oficial está vendorizado em `third_party/FidelityFX-FSR2`, com licença e proveniência registradas. A disponibilidade do SDK ou o sucesso do dispatch sintético não implica disponibilidade no gameplay: `has_fsr2` permanece zero até depth, motion vectors, jitter e artefatos temporais serem validados visualmente no jogo. FSR 3.1 não está vendorizado.
