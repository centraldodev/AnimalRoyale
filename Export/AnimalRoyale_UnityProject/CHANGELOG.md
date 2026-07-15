# Changelog

## 0.2.0

- Personagens remodelados com corpos orgânicos: os geradores do Blender agora criam uma malha contínua (metaballs convertidos) com pesos automáticos, no lugar das peças rígidas de esferas e cubos.
- Tigre: anatomia felina com peito fundo e ancas, listras pintadas na malha que seguem o corpo, focinho e barriga creme, bigodes, dedos com garras e cauda com anéis.
- Macaco: torso em pêra, braços que dobram sem emendas, cauda em curva S, máscara facial e mãos/pés claros.
- Águia: asas com penas individuais em camadas (coberteiras, secundárias e primárias), corpo aerodinâmico, cabeça branca, bico com gancho e cauda em leque.
- Formiga: exoesqueleto brilhante, cintura de pecíolo, anéis no abdômen, pernas afiladas com tarso em garra, antenas articuladas e olhos compostos.
- Novo módulo compartilhado `Tools/Blender/organic.py`; rigs, animações e o pipeline de importação do Unity permanecem os mesmos.
- Mapa mais realista: árvore com copa volumosa de tufos irregulares e tronco inclinado; arbusto com lâminas de folha e brotos; montanhas com picos facetados afunilados e vegetação nas encostas; novo pedregulho facetado com musgo (`JungleRock`) usado nas rochas soltas e nas formações escaláveis; nova flor com pétalas (`JungleFlower`).
- Mais flores no mapa (340 canteiros) em cinco cores: rosa, roxo, amarelo, vermelho e branco.
- Todos os animais agora nadam: no lago fundo o animal flutua com a cabeça fora d'água e rema (72% da velocidade); Espaço dá impulso para sair na margem.
- Rampa de pedra em anel ao redor da ilha do portal, com colisão de verdade na ilha — qualquer animal alcança o portal a pé ou nadando.
- Montanhas da águia ganharam colisão real por malha (antes o collider ficava vazio e não dava para pousar).

## 0.1.0

- Protótipo local inicial.
- Quatro animais jogáveis.
- Selva procedural.
- Bots, combate, zona segura e HUD.
- Criador automático de cena pelo menu do Unity.
