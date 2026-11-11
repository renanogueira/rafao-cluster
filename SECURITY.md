# Segurança

## Modelo de ameaça

O Rafão Cluster foi projetado para uma LAN confiável. O servidor RPC do
`llama.cpp` não deve ser tratado como um serviço público, nem colocado atrás de
um simples redirecionamento de porta.

## Recomendações

- exponha a porta RPC `50052` somente na interface da rede do cluster;
- limite o firewall de cada worker ao IP do coordenador;
- mantenha a API HTTP em `127.0.0.1`, salvo quando houver autenticação e proxy
  adequados;
- prefira VLAN ou NIC dedicada em instalações permanentes;
- mantenha sistema, drivers e `llama.cpp` atualizados;
- use tokens Hugging Face somente de leitura e nunca os coloque em logs;
- confira hashes antes de executar pacotes sem assinatura/notarização oficial.

## Relato de vulnerabilidades

Não publique tokens, endereços públicos, logs contendo dados pessoais ou
detalhes exploráveis numa issue aberta. Enquanto não houver um canal privado
dedicado, abra uma issue sem os detalhes sensíveis para solicitar contato.

## Escopo atual

Os pacotes ainda não possuem assinatura Authenticode no Windows nem
notarização Apple. Isso deve ser considerado antes de distribuição ampla.

