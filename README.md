# About

Wilbur's Quest is comprised of a few main elements:
  * Data systems for loading/building levels and performing gameplay logic on
  * Controllers and other supplementary scripts for the main game systems such as game functions, audio, input, player movement, etc.
  * Tools only used in the editor to facilitate and automate workflow

\
About 99% of all code in the game is original and developed from my own ideas. I of course had to look online for how to implement things here and there, but it was more for specific code snippets, so instead of "How do I create a device switching system?", I would search something like "How do I identify different controller brands?" so that I can create the system myself. I probably reinvented the wheel more than once, and could improve some of the systems many times over, but creating this game over the years has greatly strengthened my problem-solving skills which prevented me from looking up tutorials every step of the way. The scope of the systems in the game are pretty small, but there was rarely a point where I thought "I have no idea how to implement this." There has been a lot of iteration since the prototype phase, of which many aspects were totally rewritten, and with the architecture the game has today implementing new features and block types takes as little as a day. The most difficult parts of building the game mainly came from individual level design for making good puzzles, and designing the world pathing and room layouts as a whole due to the metroidvania nature of the game.

|Prototype (September 2020)|Current (September 2022)|
|-|-|
|![](README%20Files/prototype_version.gif)|![](README%20Files/current_version.gif)|

# Data Systems

Breakdown:
  * A [GridSystem](Systems/GridSystem.cs) holds all the information for blocks in a room in the form of [Data](Systems/Data.cs) in a 3D grid, using a [Coordinates](Systems/Coordinates.cs) position and [Layer](Controllers/GameController.cs#L11) (Each grid position can contain one block per layer)
  * Data holds information about individual blocks, such as its Coordinates position, [Tags](Controllers/GameController.cs#L10) which define if the block is pushable/floats/blocks movement/etc., its Layer, and its current state (e.g. has it been destroyed or (de)activated?)
  * Data also references a [LevelBlock](Systems/LevelBlock.cs) which holds information related to the actual gameobjects that comprise the block, and relevant components (e.g. SpriteRenderer or Light2D)
  * [BlockData](Systems/BlockData.cs) is a subset of Data holding information that is saved to disk
  * [LevelData](Systems/LevelData.cs) contains information about the individual room, blocks with changeable state, as well as the current history of block states for undoing and resetting

\
This combination of systems makes it easy to efficiently get the information I need from a block at any given time. As an example, I will describe how a button is created in-editor and used during runtime. A button is activated when a matching colored crystal is moved in front of it, or the player shoots a bullet in front of it while having that color ability.

Editor:
* The [LevelEditor](Tools/LevelEditor.cs) is loaded with a blank default GridSystem
* The button is [placed](Tools/LevelEditor.cs#L400-L426) in the editor and stored in the GridSystem as a new Data
* The level is [saved](Tools/LevelEditor.cs#L116-L126) and the button is stored as a BlockData and GridSystem [trims](Systems/GridSystem.cs#L263-L282) away unused spaces
* The level is [generated](Tools/Generation.cs#L84), and any supplementary objects for the button are [generated](Tools/Generation.cs#L776-L784) and stored in a LevelBlock, which is saved with the scene
  
Runtime:
* The GridSystem for the level is loaded and initiated with saved BlockData
* The LevelBlock and Data for the button are [linked](Controllers/GameController.cs#L4516-L4547) by matching the object's position with the data's coordinates
* The button objects are [initialized](Controllers/GameController.cs#L4694-L4704)
* Whenever the player moves, all states are [recorded](Systems/LevelData.cs#L66-L80) in LevelData
* A crystal is [moved](Controllers/GameController.cs#L718-L859) in front of the button. After the move resolves all buttons [check](Controllers/GameController.cs#L3778-L3799) the GridSystem for corresponding crystals in front of them
* The button [sets its state](Controllers/GameController.cs#L3695-L3728), and its LevelBlock is accessed to change its sprite and enable/disable its light
* All panels [update](Controllers/GameController.cs#L3934-L3944) with new button states

|Button (Object)|Button (Hierarchy)| Button Level Block (Inspector)|
|-|-|-|
|![](README%20Files/button_object.png)|![](README%20Files/button_hierarchy.png)|![](README%20Files/button_inspector.png)|

# Game Systems

### [GameController](Controllers/GameController.cs)

A static GameController is the centerpiece of all the game systems. Inside of it holds all of the general game functions, as well as [references](Controllers/GameController.cs#L252-L263) to each of the subsystems.

|GameController (Hierarchy)|GameController (Inspector)|
|-|-|
|![](README%20Files/gamecontroller_hierarchy.png)|![](README%20Files/gamecontroller_inspector.png)|

When the game is launched, a blank scene is loaded, which [initializes](Controllers/GameController.cs#L269-L355) all of the systems, creates and sets up things like [object pools](Controllers/GameController.cs#L1345-L1356) and the [worm pieces](Controllers/PlayerController.cs#L350-L431), and then immediately [loads](Controllers/GameController.cs#L358-L424) the last room the player was in. All of these objects stay attached to the GameController in the scene and follow it throughout the game using [DontDestroyOnLoad()](https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html), minimizing the loading and reinitializing of various objects. I've seen this structure referred to as a "god object" and generally bad practice. I recognize this is not ideal in a properly built game made by a full team, but as a solo developer just trying to ship a game this is what worked well for me. I feel each of the systems are sufficiently separated with their responsibilities. Even though they can all reference each other, I made sure to be careful and keep state modification to its respective system, and only make public the fields that are absolutely necessary.

A recurring element in the game systems is the use of `Function([Some kind of data], Activation activation, Activation instant = Activation.Off)` ([Button Example](Controllers/GameController.cs#L3695-L3728)), where [Activation](Controllers/GameController.cs#L15) is just an enum of Off, On, or Alt. The activation parameter is used to set state (most of which will only be On or Off), and the instant parameter denotes how the data will be modified. With this pattern I can concisely pack the logic for interacting with puzzle data and setting its state in different ways all in one. For example, when `instant == Off`, that means to modify the data as normal, play sounds, maybe play animations, etc. When `instant == On` I set the state like before, but do it instantly without any effects (useful for undoing/resetting). And when `instant == Alt` that basically means to do the same as when `instant == On`, but skip the check for identical state and match visuals to state anyway (useful for initiation from load). I usually check for a state change so that I can just send all relevant data through the logic without worrying about multiple effects applying to them. I use this pattern for practically all types of puzzle blocks, and thought about using polymorphism since a lot of the code is similar. Ultimately I found it better to have it all grouped together in the GameController as separate methods that take data instead of making classes for each type. I also could more easily see at once how different data related/interacted as the game evolved.

|Interact + Undo, Interact + Reset|
|-|
|![](README%20Files/puzzle.gif)|

#### Miscellaneous Game Scripts

* The [HeadTrigger](Misc/HeadTrigger.cs) is a collider follows the player's head and is used to detect screen borders for screen switching, activate game prompts at specific locations, and control parallaxing based on its vector from the center of the screen.
* [Screens](Misc/Screen.cs) are used to determine the size and positioning of camera focus points in each room.
* [Panels](Misc/Panel.cs) are used for locked tunnels and pistons, from which [TunnelPanel](Misc/TunnelPanel.cs) and [PistonPanel](Misc/PistonPanel.cs) inherits. A panel is activated when all of the colors depicted on it are activated by a sufficient amount of their respective colored buttons.

### [PlayerController](Controllers/PlayerController.cs)

The PlayerController controls general player input and movement. Abilities are activated here, but are mostly implemented in the [GameController](Controllers/GameController.cs) since they usually affect more elements than the player such as [eating a collectable](Controllers/GameController.cs#L1364-L1488), [undoing the last move](Controllers/GameController.cs#L1850-L1926), or [shooting at blocks](Controllers/GameController.cs#L1530-L1842).

|Player Objects (Hierarchy)|PlayerController (Inspector)|
|-|-|
|![](README%20Files/playerobjects_hierarchy.png)|![](README%20Files/playercontroller_inspector.png)|

As part of the puzzle design, I wanted the player to have their movement restricted after an action is made and wait for it to be resolved. For example, the player moves a crystal onto a button, which activates a piston, which moves a rock that falls and blocks a door. The puzzles should be dictated by pure strategy instead of any weird timing or the player being able to move somewhere really fast. At first I had many bools representing different action states and would check for them all like `if (blocksMoving || applyingGravity || playerMoving || etc.) then return from player input`. This got more complex as the game evolved and more features were added. I eventually came up with [FlagGameState(bool flag)](Controllers/GameController.cs#L613-L616), which I could use everywhere instead of separate states. Any time I needed to suspend player input I would use `FlagGameState(true);` at the start of an action and `FlagGameState(false);` at the end. This way I could keep track of individual actions and even chain them together without worrying about the order of states, and then check for `if (!resolvedGameState) then return from player input`.

The logistics for player animations were a fun challenge to overcome. Since the player's length can change I needed a modular way to move each piece of the worm to the next spot. I accomplish this by [directly moving the gameobject of pieces that move straight and animating pieces that turn a corner](Controllers/PlayerController.cs#L951-L1027). The movements are synced and step by one pixel each time to keep consistency with the pixel art positioning. To give the worm some personality I also wanted a variety of face animations. The face consists of a separate eye, head, and mouth object. There are animations for each that can [play independently from each other, as well as animations that are meant to be synced together](Controllers/PlayerController.cs#L2237-L2325). For this I used a custom approach, [AnimationData](Controllers/PlayerController.cs#L148-L159), instead of Unity's built in animation system, as I didn't need a lot of the overhead and it was much easier to control the syncing in code.

| |Player Animations| |
|-|-|-|
|![](README%20Files/player_blink.gif)|![](README%20Files/player_sleep.gif)|![](README%20Files/player_nod.gif)|
|![](README%20Files/player_shoot.gif)|![](README%20Files/player_sing.gif)|![](README%20Files/player_move.gif)|
|![](README%20Files/player_think.gif)|![](README%20Files/player_grow.gif)|![](README%20Files/player_eat.gif)|
|![](README%20Files/player_fall.gif)|||

### [MapController](Controllers/MapController.cs)

The MapController handles [room pathing generation](Controllers/MapController.cs#L601-L980) and [map display](Controllers/MapController.cs#L1062-L1158). The map is simply a texture that I write on with colored pixels to represent different blocks, where 1 pixel = 1 block.

|MapUI (Hierarchy)|MapController (Inspector)|
|-|-|
|![](README%20Files/mapui_hierarchy.png)|![](README%20Files/mapcontroller_inspector.png)|

|Map|
|-|
|![](README%20Files/map.gif)|

The map is one of my favorite features in the game. I wanted a minimalistic, but accurate representation of the world so players could easily identify previous rooms. I also wanted the map to animate in a way that looked like it was branching out from the current room to all visited areas. The map probably went through the most iterations and optimizations to get where it is today. I had initially represented each pixel with a separate gameobject and recalculated the pathing every time the map was opened. This became way too slow as the map got bigger, and led me to researching about Texture2D. In addition to using a single texture in place of hundreds of gameobjects, the map system now uses a lot of pre-generation to keep runtime logic as efficient as possible. This new system also allowed me to have more detailed pathing. I wanted to have a flood-fill effect through the rooms, but it would be way too computationally expensive to calculate it every time the map was opened in the first system. With the new system I could generate the flood-fill separately for each room. I [created a different path to be used from each tunnel](Controllers/MapController.cs#L601-L980), so that no matter which one was traveled through by the branching, it would properly propagate through the room. The coordinates for each pixel were stored in the order they were discovered, so that when I calculate the full path, I just had to [link these coordinates in the right order depending on when the tunnel was reached](Controllers/MapController.cs#L463-L538).

I couldn't pre-generate all of the blocks in a room since their position or state could change, but I could still draw the base room blocks such as the ground outline and empty space. [Each time the map is opened, only the current room's pixels are rewritten using the template sprite, and then the mutable blocks are rewritten on top of that](Controllers/MapController.cs#L1165-L1310). After the player exits a room the map writes the state for the last time, which will end up being permanent since the room pixels won't be modified once the player is no longer in that room. When the map is [displayed](Controllers/MapController.cs#L1062-L1158), the texture that actually shows starts blank, and copies the master map in the order of the pre-generated coordinates that were linked when the room loaded to get the proper pathing. So after all pre-generation and calculation is done, basically the only work that needs to happen at runtime is reading the ordered array of coordinates and copying one texture to another. When a room is entered path connections are created, and when a room is exited the flood-fill is regenerated (because previously unvisited tunnels or newly broken walls could open up new pathways).

### [InputController](Controllers/InputController.cs)

Input is initially handled with Unity's [InputSystem](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.4/manual/index.html), which is then read by my custom controller. Within the InputController I control the [sprites used for button images](Controllers/InputController.cs#L321-L335), [device switching](Controllers/InputController.cs#L214-L249), and [button prompts](Controllers/InputController.cs#L444-L569). Whenever the player collects a new ability, one or more [ControlPrompts](Misc/ControlPrompt.cs) appear that mimic what buttons they should press to activate it.

|InputController (Inspector)|
|-|
|![](README%20Files/inputcontroller_inspector.png)|

|Device Switching|
|-|
|![](README%20Files/deviceswitching.gif)|

An important aspect I wanted in the game was for it to be completely language independent (except for the menu unfortunately). Another big design principle I believe in is that players should discover as much of the game as possible themselves instead of having things explained to them. When players collect a new ability, I didn't want a pop-up saying "You just collected \_\_\_! Press ___ and \_\_\_ to ___." I wanted a modular way to present multiple prompts that activated automatically and responded to player input. To accomplish this I came up with the [ControlPrompt](Misc/ControlPrompt.cs). Most abilities require a primary button to be held and then a movement direction to be pressed, but some of them only need a single button to be pressed or held. Using a similar [3D popout effect](Misc/ControlPrompt.cs#L212-L256) as the menu, I have a prompt appear that [mimics](Misc/ControlPrompt.cs#L168-L210) which buttons should be pressed and with which timing. All other controls are [locked](Controllers/InputController.cs#L571-L587) during this time so the only way to proceed is to successfully complete the prompt, after which they will automatically discover what the actual ability does. This also kept in theme with the puzzles since if it wasn't immediately obvious what the ability was, the player is still free to experiment with it afterwards.

|Grow Ability Prompt|
|-|
|![](README%20Files/prompt.gif)|

One QOL feature I wanted in the game was the option to toggle the buttons used in a combo prompt. For example, instead of holding A and pressing a direction to grow, the player could press A, then press a direction to grow, then press A again to stop the growing action. This way if for whatever reason the player wanted to have to press only one button at any given time throughout the game they could. With the previous system I was directly reading the values of buttons to determine if they were pressed or not, but for a toggle the values won't match with what the intended action is. I had to come up with a slightly different [method](Controllers/InputController.cs#L175-L202) where the [PressType](Controllers/InputController.cs#L7) was stored and I could change that using both values and toggle settings. Then I [read](Controllers/InputController.cs#L157-L173) the press type instead of the raw values when I want to actually check for a button press.

### [MenuController](Controllers/MenuController.cs)

To simplify the UI work I had to do, the main menu and pause menu are essentially the same. All of the sub-menus are offscreen and [scroll into place](Controllers/MenuController.cs#L726-L789) when selected. The main [button effect](Controllers/MenuController.cs#L1083-L1125) uses offset sprites to make the buttons look like a 3D popout.

|MenuUI (Hierarchy)|MenuController (Inspector)|
|-|-|
|![](README%20Files/menuui_hierarchy.png)|![](README%20Files/menucontroller_inspector.png)|

|Menus|
|-|
|![](README%20Files/menu.gif)|

I used a custom solution here instead of Unity's built in UI system, mostly because of the types of effects I wanted to implement and easier overall control. The system at the top level is an array of [Menus](Controllers/MenuController.cs#L10-L18), each of which have an array of [Items](Controllers/MenuController.cs#L20-L76) (vertical selections), each of which have an array of [GameObjects](Controllers/MenuController.cs#L25) (horizontal selections). Each Item has an [ItemType](Controllers/MenuController.cs#L22) such as [Slider](Controllers/MenuController.cs#L39-L47), [Options](Controllers/MenuController.cs#L48-L52), [CheckBox](Controllers/MenuController.cs#L53-L56), etc. This was a prime situation to use polymorphism, but the Unity editor isn't able to distinguish between the different types, so I couldn't link up all the objects by dragging them in the inspector using that method. Instead I just denote the type with an enum and have to leave unused types as null.

### [AudioController](Controllers/AudioController.cs)

All sound effects are [referenced](Controllers/AudioController.cs#L119-L176) in the AudioController for easy access. Sounds are played through the camera, which gives no spacial reference for where they're coming from. This is intended, but there are certain objects that I *do* want to sound different based on player position (e.g. collectables), so I use [PositionalAudio](Controllers/AudioController.cs#L20-L32) to [change the volume of sounds depending on the closest object of each group](Controllers/AudioController.cs#L343-L374).

|AudioController (Inspector)|
|-|
|![](README%20Files/audiocontroller_inspector.png)|

### [ParticlePooler](Controllers/ParticlePooler.cs)

A general object pooler wasn't very useful since there are very few objects that needed pools (e.g. bullets), but different particles are used in many areas of the game. The ParticlePooler allows for easy access to available particles to be [played](Controllers/ParticlePooler.cs#L46-L59) and not have to worry about destroying anything afterward.

|ParticlePooler (Inspector)|
|-|
|![](README%20Files/particlepooler_inspector.png)|

# Tools

*Quick Disclaimer: Any error messages seen in the upcoming examples are not actual errors, but are my own Debug.LogError() messages intended to distinguish established system reports from print messages meant for temporary debugging.*

### [LevelEditor](Tools/LevelEditor.cs)

The LevelEditor allows me to easily create and iterate on the rooms of the game. When I want to make a new room, I make a copy of the template scene that has the barebones items included. The system is set up so that switching between playing the room as normal and starting up the level editor is controlled simply by deactivating the level editor object before hitting the play button.

When editing I can [place](Tools/LevelEditor.cs#L400-L426), [delete](Tools/LevelEditor.cs#L427-L466), and [move](Tools/LevelEditor.cs#L467-L575) blocks on different [Layers](Controllers/GameController.cs#L11). Groups of blocks can be [toggled](Tools/LevelEditor.cs#L272-L279) for better visibility of blocks on different sorting layers. I can also edit the [Screen](Misc/Screen.cs) regions and [view](Tools/LevelEditor.cs#L349-L371) the room through them to get an accurate representation of what it will look like in-game. Blocks are represented by their basic colors as a generic sprite, and get prettied up when the room is generated. Clicking and dragging blocks will [connect](Tools/LevelEditor.cs#L577-L605) them together if applicable when the mouse button is let go. Tilable blocks will [update their tiling](Tools/LevelEditor.cs#L606-L616) with neighbors. Adding a new block to the game is very simple. First I make an entry for it in [Assets](Systems/Assets.cs) and make sure a prefab is set up, then I just need to add its name to the [hotbar](Tools/LevelEditor.cs#L60-L66).

|Level Editor|
|-|
|![](README%20Files/leveleditor.gif)|

### [Generation](Tools/Generation.cs)

After creating a room in the [LevelEditor](Tools/LevelEditor.cs), Generation is used to automate any and all setup so I can jump right into the game and test it afterwards. This brings the room to not only just a testable state, but it will also look and play exactly like the finished shipped product with a single click.

|Generating + Playing|
|-|
|![](README%20Files/generation.gif)|

First, all of the old objects are destroyed to generate fresh from the existing/new save data. Any relevant parameters that aren't held in the save data are copied and carried over. Additional ground blocks are also [generated](Tools/Generation.cs#L233-L312) to fill up the screen, so I only have to draw an outline of the room shape in the level editor. The blocks are [sorted](Tools/Generation.cs#L353-L435), then sprites are created using [SpriteGeneration](Tools/SpriteGeneration.cs) and all supplementary objects for blocks are [created](Tools/Generation.cs#L437-L797) such as outlines that flash when undoing. References for these objects and other components are stored if they need to be accessed during runtime in a [LevelBlock](Systems/LevelBlock.cs).

The blocks were initially generated as separate gameobjects with set randomized sprites from a custom tilemap system. To cut down on the amount of game objects and deal with other weird sprite/camera artifacts I came across, I overhauled the system so that one sprite for each block group is created. For example, all ground blocks are built into one sprite, rocks and crystals are built together in groups so they can move separately, and background blocks of each layer are grouped together for parallaxing. Having so many unique sprites would usually be bad for optimization and file sizes, but I can get away with it for this game because the resolution is so small (average block is 7x7 pixels, and average room size is 30x15 blocks). This new method fixed some of the aforementioned issues, and it also allowed me to be more fancy with the sprite randomization. Since every sprite was now unique, I could give them completely random patterns so that the game didn't look so samey. Usually in a metroidvania different areas have completely different themes, but I knew that I would never have the time to create something that ambitious. More random generation definitely helped make the game less boring looking, and in a way also made it feel more like a natural connected world instead of separate game scenes made with repeating tiles.

Next the shadow effect that appears along the edge of the room is [generated](Tools/Generation.cs#L1185-L1295). This helps to focus the gameplay area and not have so much brown ground coloring on the screen. I start with the [pre-marked](Tools/Generation.cs#L477-L490) empty spaces of the ground sprite that light should emanate from. I also wanted shadows to appear on certain blocks like tunnels that are basically integrated with the ground, but they use a separate sprite. To deal with this I created [PixelGroup](Tools/Generation.cs#L55-L68). Using this I can now determine which pixels are overlapping by getting the offsets between gameobjects, sprites, and texture coordinates so that I can apply the same shadow level to pixels that are in the same position, and group them together so they can more easily be colored at the same time. A flood-fill method is used to expand outwards from the initially marked empty pixels and as the distance gets further away the pixels are darkened more. Since all the sprites are uniquely created for each room I can draw the shadow pixels directly onto them instead of a separate object.

|Pre-Shadow|Post-Shadow|
|-|-|
|![](README%20Files/preshadow.png)|![](README%20Files/postshadow.png)|

After that two room maps are [generated](Tools/Generation.cs#L1497-L1753). One represents the full room including colored blocks, icons, and tunnels, as well as the screen borders. This sub-map is only for use in the editor and creating the full map. Having the full representation lets me easily review the big picture of the game and plan object locations and room connectivity. The second map is the base template of the room that is used in the [MapController](Controllers/MapController.cs) to update the in-game map as the player modifies the room, of which the mutable blocks are then written on top of.

|Room|Editor Map|Template Map|
|-|-|-|
|![](README%20Files/roommap_room.png)|![](README%20Files/roommap_editor.png)|![](README%20Files/roommap_template.png)|

Lastly, the support crystals are [generated](Tools/Generation.cs#L1297-L1495). These crystals are present in the background of each room and have a pulsing glow effect in the direction of their respective color ability. The supports are meant to be root-like structures that grow outward from the color flowers throughout the world. I wanted this feature because it adds something interesting to the room, and also gives the player hints of where to go if they become lost. For them to be generated the room has to first be included in the main world map, because otherwise it has no reference for its position in relation to the rooms that contain the color abilities. The support blocks are divided evenly between the colors for available space, but then those divisions are populated with a percentage of crystals depending on the global distance from the current room to each color ability room. There is also some additional logic to make the randomness prettier and make sure that crystals can actually be seen instead of being generated behind a background object. As the player gets closer to a certain color ability, the surrounding rooms' respective crystals will become more numerous and tend to be bigger.

|Crystals In-Game|Pre-Crystals|Post-Crystals|
|-|-|-|
|![](README%20Files/supportcrystals.gif)|![](README%20Files/precrystals.png)|![](README%20Files/postcrystals.png)|

### [MapEditor](Tools/MapEditor.cs)

The MapEditor allows me to set and change the placement of rooms in relation to each other, and dictates which tunnels connect each room.

|Map With Info Enabled|Map With Info Disabled|
|-|-|
|![](README%20Files/map_withinfo.png)|![](README%20Files/map_withoutinfo.png)|

Using the previously generated room map sprites, I can drag them around right in the Unity editor as I would any gameobject. Tunnel endings are denoted by pink pixels, and all I have to do to connect two rooms is place them so that two tunnel endings are next to each other. There is one issue with this, as I now have to build the tunnels of each room to exactly line up, but its actually not a problem. Since the map is a perfect 1:1 representation of the rooms, I can set the scale of it to match the actual room while in the level editor and just draw the tunnel to where it needs to go. Of course I sometimes have to go back and modify the tunnels when I decide to move rooms around, but that's all a part of iteration.

|LevelEditor + MapEditor|
|-|
|![](README%20Files/leveleditorwithmapeditor.gif)|

All the rooms are generated and positioned according to their [RoomData](Controllers/MapController.cs#L159-L194), which is a part of the overall [MapData](Controllers/MapController.cs#L6-L157). All rooms that have a room map are also included, but deactivated. This way I can activate and deactivate any rooms in the game for easy swapping and iteration. When the map is saved, all tunnels look for adjacent connections, and the individual room sprites are copied to their set position on a full map sprite.

|Adding To Map + Playing|
|-|
|![](README%20Files/addtomap.gif)|

### [SpriteGeneration](Tools/SpriteGeneration.cs)

SpriteGeneration is where the randomized patterns and block shapes are generated.

|SpriteGeneration (Inspector)|
|-|
|![](README%20Files/spritegeneration_inspector.png)|

Each block type has a [SpriteInfo](Tools/SpriteGeneration.cs#L7-L45) which uses a basic color, as well as one or more tiling and pattern sprites to generate from. They also have [SpriteTags](Tools/SpriteGeneration.cs#L5) to determine if the sprite should apply things like random divoting, excluding the outline, or if the patterns should follow the directional flow of the blocks like a pipe would. 

|Tiling With Variations|Tunnel Patterns|
|-|-|
|![](README%20Files/tiling.png)|![](README%20Files/patterns.png)|

The tiling sprites use different colors to define the shape of the block in different variations according to the [sprite tiling](Systems/Assets.cs#L120-L156). Blue pixels signify the outline, cyan pixels signify areas the surface can divot inward, and red pixels signify the bulk of the shape. First a sprite is built by using the sprite state previously set from [ApplyTiling()](Controllers/GameController.cs#L1177-L1248) to copy each tiling type over. Using the basic color, a palette of shades is generated to use for the patterns. Then from left to right and top to bottom random patterns are picked and given a random shade and rotation. The patterns are written to a second sprite which follows the color coding of the first built sprite, so when the pattern overlaps blue pixels they get a brighter shade than when it overlaps red pixels. This second sprite then gets applied to the object when finished.

|Ground With Tiling Applied|Ground With Patterns Applied|
|-|-|
|![](README%20Files/tiletexture.png)|![](README%20Files/spritetexture.png)|
