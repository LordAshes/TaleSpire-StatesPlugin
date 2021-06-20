# States Plugin

This unofficial TaleSpire plugin is for adding lines of text above a mini
to provide visible information such a mini states (e.g. poisoned, frozen, etc).

![Preview](https://i.imgur.com/TO8Cvjx.png)

## Change Log

2.1.0: Added access to states from the mini radial menu. Select Info in the main menu and then states.
2.0.2: Fixed Thunderstore dependency in manifest
2.0.1: Plugin is now listed on the TaleSpire main page
2.0.1: Fixed bug which prevented modifying existing states unless the states were first removed
2.0.0: Rewrite to use Stat Messaging for higher compatibility with other plugins
1.1.0: Added support for rich text
1.1.0: Added keyword colorization json file
1.1.0: Added base text exposed in configuration
1.0.0: Initial release

## Install

Install using R2ModMan or similar. Exapand the TaleSpire_CustomData into your TaleSpire game directory. The TaleSpire_CustomData is not
part of the core game. It is used by Lord Ashes' plugins to hold files related to additional content and plugins. If the folder already
exists (due to other plugins being installed) merge the contents togteher. This will make the necessary radial menu icons available.

## Usage

Select a mini and then press the States shortcut keys (defaults to CTRL+S but can be changed in R2ModMan) to activate the Stats dialog.
Enter some text. If there are commas in the text, they will used to separate the text into multiple lines. The intention of this plugin
is to add information about the character states/conditions such as Poisoned, Exhausted, Unconcious, On Fire, etc. However, the plugin
can be used to display any text so it could be use for many other purposes.

To ensure compatability with Custom Mini Plugin, the text should not contain any colon (:) characters.

## Stealth Mode

The plugin synchronizes the text with the mini's stealth mode. When the mini is hidden due to Stealth, the text will also be hidden.

## Three Ways To Color Your World

### Base Text Color Configruation

In the States Plugin section of R2ModMan the base text color can be configured. When entering a number in R2ModMan for the color the
code needs to be a 8 digit code preceeded by a hash sign in the form of #RRGGBBAA. Each of the two characters represent the hex value
of the byte for the red color, green color, blue color and the alpha (FF=Opaque, 00=Full Transparent).

### Colorization Keywords

Keywords can be colorized by providing a JSON colorization file. The JSON colorization file is expected in following folder with
the following file name:

```
\Steam\steamapps\common\TaleSpire\TaleSpire_CustomData\Config\org.lordashes.plugins.states\ColorizedKeywords.json
```

The contents of the file has the following format:

```JSON
{
	"Poisoned": "<#00FF00>Poisoned<Default>",
	"Frozen": "<#FFFFFF>Frozen<Default>",
	"Shocked": "<#5555FF>Shocked<Default>",
	"Exhausted": "<#FF7733>Exhausted<Default>",
	"On Fire": "<#FF0000>On Fire<Default>",
	"<Default>": "<#000000>"
}
```

The first entry per line (the key) indicates the keyword that is replaced when found in the specified text.
The second entry per line (the value) indicates what the keyword gets replace with. Typically the replacement
includes a color prefix in the form of <#RRGGBB> or <#RRGGBBAA>. To ensure non-replaced text is colored with
the default color, each entry ends in <Default> and the value of <Default> is the last entry in the file.
<Default> must be the last entry in the file since replacements are applied in order. Making it the last entry
ensures that all other replacements which have it at the end, will get properly resolved.

When using this color method, the Base Text Color configured in R2ModMan is ignored and the <Default> color is
used instead.

### Manual Colorization

When specifying text into the CTRL+S dialog, colors codes can be applied manually by entering text in the form
of <#RRGGBB> or <#RRGGBBAA>. When such text is entered, it is automatically removed and processed (i.e. text
entered after it will appear in the specified color).
