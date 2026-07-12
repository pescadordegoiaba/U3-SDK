# Licenças do Plugin Linux FidelityFX

Este diretório contém a ponte ABI local e o compute smoke Vulkan do U3-SDK. O smoke usa somente headers oficiais da Unity, Vulkan e um shader GLSL próprio do projeto; não contém código FidelityFX da AMD.

FSR 1 é implementado no shader Unity do projeto. Código oficial AMD usado como referência deve manter o aviso MIT da AMD quando incorporado.

FSR 2/FSR 3.1 não estão vendorizados nesta revisão. A disponibilidade do bridge Vulkan não implica disponibilidade de FSR 2: `has_fsr2` permanece zero até o SDK oficial, o dispatch temporal e seus inputs serem integrados e validados.
