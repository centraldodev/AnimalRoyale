# Fontes mestras dos animais

Os quatro arquivos `*_Rig_Master.blend` desta pasta são as fontes editáveis dos
animais atualmente usados no Unity. Cada arquivo contém:

- armature e hierarquia completa de ossos;
- malha vinculada ao rig por skin weights;
- textura base 2K empacotada no próprio `.blend`;
- pose de descanso limpa, sem Actions, NLA ou keyframes antigos.

Crie as novas animações diretamente nesses arquivos. Os FBX consumidos pelo Unity
continuam em `Assets/AnimalBattleRoyale/Art/Characters/<Animal>/Models`.

Para reconstruir os mestres a partir dos FBX atuais:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --python Tools/Blender/create_animal_master_blends.py
```

O script sobrescreve os quatro `.blend`; portanto, não o execute depois de começar
a criar animações sem antes guardar as alterações.
