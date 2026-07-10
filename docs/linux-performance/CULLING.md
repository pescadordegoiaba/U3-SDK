# Culling

O projeto já usa layer culling, LOD bias, LODGroup, culling volumes, region visibility e foliage frustum culling.

Esta revisão adiciona perfil básico/agressivo que reduz distâncias por camada após `GraphicsSettings.apply`.

HZB permanece experimental e desligado porque requer validação visual para evitar popping.
