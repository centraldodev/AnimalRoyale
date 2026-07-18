# Changelog

- A renderização 3D recebeu gradação cartoon compartilhada entre o preview do menu e a partida, com mais saturação e contraste, luzes menos estouradas e névoa/reflexos mais equilibrados; o fundo estático e a interface mantêm suas cores originais.
- Menu inicial reconstruído sobre o novo plano de fundo estático, sem renderizar o mapa: animal 3D de corpo inteiro com seletor de vestuário, botão de partida, resumo da sala para 15 participantes, painel lateral rolável com volumes e todas as configurações existentes, descoberta/convite automático entre jogadores na mesma rede local e atalhos inferiores para Notícias, Créditos e Sair (somente Sair ativo nesta etapa).
- Quatro fontes mestras editáveis do Blender foram criadas a partir dos animais rigados atuais, com armature, skin weights e textura 2K empacotada, mas sem Actions/NLA antigos; após a validação dos quatro arquivos, os 206 MB de fontes brutas da pasta `ArtRefs` foram removidos do projeto.
- As animações antigas dos quatro animais foram removidas para permitir uma reconstrução do zero: nenhum idle, corrida, salto, giro, ataque, poder ou recoil altera mais os modelos; malhas com skin, armatures e todos os ossos continuam preservados, enquanto o deslocamento e o combate do jogo permanecem funcionais.
- O novo áudio estéreo de 8 segundos substitui toda a mixagem ambiente padrão durante as partidas; as camadas antigas continuam armazenadas no projeto, mas não são mais carregadas ou reproduzidas, e a aproximação do pântano mantém o crossfade gradual existente.
- Novo bioma no canto sudeste: lago de pântano alongado com bacia escavada no próprio terreno, água verde-escura, margem lodosa, cerca de 40 árvores exclusivas usando os três novos modelos e a nova casa posicionada na entrada; árvores, grama, flores, rochas e montanhas comuns respeitam uma área reservada ao redor do pântano.
- O pântano ganhou ambiente sonoro próprio em estéreo: a aproximação inicia uma transição suave de 4 segundos, reduzindo o som normal sem cortá-lo e elevando o novo loop; ao sair, a mixagem retorna gradualmente ao ambiente da floresta.
- Árvores, rochas e montanhas do novo ambiente agora usam a altura exata dos triângulos renderizados e ficam parcialmente encaixadas no terreno conforme o próprio tamanho, eliminando bases flutuantes sem achatar o relevo do mapa; as pedras pequenas da margem permanecem inalteradas.
- Novos sons integrados: a cachoeira central agora reproduz um loop espacial que aumenta suavemente ao se aproximar da ilha, e a recarga usa o novo efeito de 2 segundos apenas para o jogador local, evitando sobreposição das recargas dos bots.
- Nova vegetação cartoon leve: cerca de 10.400 tufos de grama e 280 flores em três formatos distintos (margaridas brancas, flores-estrela rosas e flores roxas), organizados em setores sem colisão para preservar desempenho e navegação; o FBX `grama3D` original permanece apenas como referência por ter quase 2 milhões de triângulos.
- O piso do mapa agora usa uma cópia otimizada de 810 KB da textura verde 2K `ground01` do Nature Starter Kit 2, repetida e tonalizada para combinar com o visual cartoon; o package completo de 178 MB não entra nos Resources nem no build.
- A faixa de terra ao redor do lago deixou de ser uma argola plana: sua nova malha usa 1.161 pontos e interpola exatamente os mesmos triângulos renderizados pelo terreno, acompanhando curvas, morros e desníveis sem flutuar, afundar ou produzir recortes quebrados.
- A malha `pedralago` foi dividida em 18 variações representativas das suas 135 pedras; aproximadamente 300 pedras sem colisão agora ocupam três faixas sobre toda a margem bege, acompanhando altura e inclinação do terreno e preservando o corredor da ponte.
- As pedras da margem foram reduzidas pela metade e agora afundam de 2,5 a 7,5 cm conforme o próprio tamanho, mantendo a inclinação da superfície para um encaixe mais natural na terra.
- A ponte modular do lago foi rebaixada em 65 cm para assentar os pilares e as duas rampas na margem irregular sem desalinhamento entre as cinco peças.

## 0.2.0

- Personagens remodelados com corpos orgânicos: os geradores do Blender agora criam uma malha contínua (metaballs convertidos) com pesos automáticos, no lugar das peças rígidas de esferas e cubos.
- Tigre: anatomia felina com peito fundo e ancas, listras pintadas na malha que seguem o corpo, focinho e barriga creme, bigodes, dedos com garras e cauda com anéis.
- Macaco: torso em pêra, braços que dobram sem emendas, cauda em curva S, máscara facial e mãos/pés claros.
- Águia: asas com penas individuais em camadas (coberteiras, secundárias e primárias), corpo aerodinâmico, cabeça branca, bico com gancho e cauda em leque.
- Formiga: exoesqueleto brilhante, cintura de pecíolo, anéis no abdômen, pernas afiladas com tarso em garra, antenas articuladas e olhos compostos.
- Novo módulo compartilhado `Tools/Blender/organic.py`; rigs, animações e o pipeline de importação do Unity permanecem os mesmos.
- Mapa mais realista: árvore com copa volumosa de tufos irregulares e tronco inclinado; arbusto com lâminas de folha e brotos; montanhas com picos facetados afunilados e vegetação nas encostas; novo pedregulho facetado com musgo (`JungleRock`) usado nas rochas soltas e nas formações escaláveis; nova flor com pétalas (`JungleFlower`).
- Novo ambiente rochoso criado com os nove modelos fornecidos: 15 montanhas/formações gigantes de 34–70 m no horizonte e 16 rochas de 9–28 m em anéis internos, mantendo o centro da arena aberto. Os prefabs usam texturas 2K com mip streaming, materiais compartilhados, colisores e suporte ao NavMesh em builds.
- Reconstrução natural do mapa com lago central navegável, até 256 árvores e 32 cogumelos. Dois anéis irregulares adensam as margens e três faixas adicionais prolongam a mata até os morros e montanhas das bordas, sem bloquear o nascimento do jogador nem os acessos da ponte; a ilha com cachoeira permanece no centro e a ponte modular usa duas cabeceiras próprias com três repetições da peça central, sem esticar os modelos.
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
