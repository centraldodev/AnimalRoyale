# Animal Battle Royale — Unity Starter

Starter local em terceira pessoa para um battle royale 3D de animais em uma selva procedural.

## Versão recomendada

- Unity 6000.3.19f1 ou uma versão Unity 6 mais recente.
- Template: **Universal 3D (URP)** ou **3D Core**.
- O código aceita tanto o novo Input System quanto o Input Manager antigo.

## Como instalar

### Opção A — copiar para um projeto novo

1. No Unity Hub, crie um projeto **Universal 3D**.
2. Feche o Unity.
3. Copie a pasta `Assets/AnimalBattleRoyale` deste pacote para a pasta `Assets` do projeto.
4. Abra o projeto novamente.
5. No menu superior, clique em **Animal Battle Royale > Create Starter Scene**.
6. Abra `Assets/AnimalBattleRoyale/Scenes/Prototype.unity` e pressione **Play**.

### Opção B — importar apenas os scripts

Copie `Assets/AnimalBattleRoyale` para qualquer projeto Unity 6 existente. O gerador não depende de modelos ou assets externos.

## Controles

- **WASD**: movimentar
- **Mouse**: controlar câmera
- **Shift**: correr
- **Espaço**: pular / subir durante o voo da águia ou escalada do tigre
- **Ctrl ou C**: descer durante o voo ou escalada
- **Q**, **E**, **R**: ativar os três poderes do animal
- **Clique esquerdo**: sempre executar o ataque-base
- **F**: consumir o alimento ou cristal mais próximo
- **Clique direito**: mira precisa; direciona o ataque pela câmera e mostra a mira central
- **Esc**: liberar ou prender o cursor

## Animais e habilidades

- **Formiga** — Ataque-base: morde e arremessa o alvo, com o maior empurrão e 12% de resistência natural. Poderes: Escudo Quitinoso, Túnel Subterrâneo (E entra num buraco próximo e emerge em outra saída) e Fúria da Colônia.
- **Macaco** — Ataque-base: Soco Duplo. Possui salto duplo contínuo para atravessar obstáculos e árvores. Poderes: Salto de Cipó (Q procura cipós nas árvores próximas), Impacto da Selva e Fúria Primal.
- **Tigre** — Ataque-base: Mordida forte; pode pular normalmente a qualquer momento. Poderes: Salto Predador, Garras Escaladoras e Rugido Dominante.
- **Águia** — Espaço inicia voo livre sem recarga. Ataque-base no ar: Razante de Garras, que causa dano durante o mergulho. Poderes: Corrente Ascendente, Rajada de Vento e Pouso Preciso pela mira.

## Sistemas já incluídos

- Controle em terceira pessoa com colisão e gravidade.
- Câmera orbital com prevenção de atravessar árvores e objetos.
- Arena procedural de 360 m, com solo irregular de colinas, vales e depressões para cobertura natural, além de árvores altas, arbustos grandes, rochas, formações escaláveis, formigueiros gigantes e 16 casas abertas para abrigo.
- Quase metade das árvores possui cipós utilizáveis pelo Macaco; 30 entradas de túnel conectam a rede subterrânea da Formiga; seis montanhas altas servem de rota e poleiro para a Águia.
- Alimentos temáticos de cura (carne para o Tigre, peixe para a Águia, fruta para o Macaco e néctar para a Formiga) e cristais de escudo espalhados pelo mapa.
- 16 participantes por padrão: 1 jogador e 15 bots.
- Combate corpo a corpo, vida, dano, eliminação e vitória/derrota.
- Área segura circular que diminui; fora dela, a chuva ácida causa 10 de dano por segundo e exibe gotas tóxicas no animal.
- Todos os animais nadam no lago central (flutuação automática; Espaço dá impulso), e uma rampa de pedra circular leva à ilha do portal.
- IA local simples: procura inimigos, ataca, usa habilidades e volta para a zona.
- Tela de seleção de animal antes da partida.
- HUD de teste sem dependências de UI, incluindo barras de vida e escudo.
- Feedback de combate: sons procedurais distintos, cortes/ondas coloridos por animal, impacto no alvo e números de dano flutuantes somente quando o golpe acerta.

## Direção de arte

A referência visual original do ambiente está em `Assets/AnimalBattleRoyale/Art/Concepts/EnvironmentArtDirection.png`. O objetivo é uma selva 3D cartoon saturada, com frutos, cipós, cristais, flores, nuvens volumosas, casas de madeira e quedas-d'água. A geração procedural já cria versões jogáveis desses elementos; a evolução seguinte é substituí-las por prefabs modelados e animados.

Os personagens usam corpos orgânicos gerados por `Tools/Blender/create_<animal>.py` com o módulo `Tools/Blender/organic.py`: uma malha contínua com pesos automáticos e detalhes (olhos, garras, penas) presos aos ossos. Para regenerar um animal, rode por exemplo `Blender --background --python Tools/Blender/create_tiger.py -- Assets/AnimalBattleRoyale/Art/Characters/Tiger/Models` e deixe o Unity reimportar.

## Balanceamento do protótipo

O ataque-base é sempre imediato para manter o ritmo. As recargas existem apenas nos poderes de Q/E/R. A Formiga controla posição, o Macaco mantém pressão móvel, o Tigre vence em curto alcance e a Águia usa mobilidade aérea para escolher o combate.

## Estrutura

```text
Assets/AnimalBattleRoyale/
├── Editor/
│   └── StarterSceneCreator.cs
├── Scenes/
└── Scripts/
    ├── AI/
    ├── Camera/
    ├── Combat/
    ├── Core/
    ├── Player/
    └── World/
```

## Próximas etapas recomendadas

1. Substituir os animais de formas geométricas por modelos com Animator.
2. Adicionar itens, sons, partículas e feedback visual de dano mais elaborado.
3. Transformar a selva procedural em Terrain com LOD e vegetação instanciada.
4. Adicionar lobby e multiplayer autoritativo com servidor.

## Observação de desempenho

O cenário usa formas primitivas para facilitar o primeiro teste. Para mobile ou mapas maiores, troque a vegetação por prefabs com LOD, GPU instancing e object pooling.
