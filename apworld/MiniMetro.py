"""
Mini Metro World for Archipelago
"""

from typing import List
from dataclasses import dataclass
import json
import pkgutil
from BaseClasses import Region, Entrance, Item, Location, ItemClassification, MultiWorld
from worlds.AutoWorld import World
from worlds.generic.Rules import set_rule
from Options import Range, Choice, DeathLink, PerGameCommonOptions

_ITEM_NAME_TO_ID = json.loads(pkgutil.get_data(__name__, "data/items.json").decode())
_LOCATION_NAME_TO_ID = json.loads(pkgutil.get_data(__name__, "data/locations.json").decode())

MAPS = {
    "London": 1,
    "Paris": 2,
    "New York City": 3,
    "Warsaw": 4,
    "Lisbon": 5,
    "Tokyo": 6,
    "Chicago": 7,
    "Budapest": 8,
    "Berlin": 9,
    "Melbourne": 10,
    "Hong Kong": 11,
    "Barcelona": 12,
    "Osaka": 13,
    "Stockholm": 14,
    "Saint Petersburg": 15,
    "Boston": 16,
    "Montreal": 17,
    "San Francisco": 18,
    "Sao Paulo": 19,
    "Seoul": 20,
    "Santiago": 21,
    "Washington, D.C.": 22,
    "Tashkent": 23,
    "Singapore": 24,
    "Cairo": 25,
    "Istanbul": 26,
    "Shanghai": 27,
    "Guangzhou": 28,
    "Nanjing": 29,
    "Chongqing": 30,
    "Mumbai": 31,
    "Addis Ababa": 32,
    "Lagos": 33,
    "Auckland": 34
}

ITEMS = {
    "Interchange - Unlock": ItemClassification.progression,
    "Shinkansen - Unlock": ItemClassification.progression,
    "Tunnel/Bridge - Unlock": ItemClassification.progression.early,
    "Carriage - Unlock": ItemClassification.progression,
}

HELPER_ITEMS = {
    "Extra Locomotive": ItemClassification.useful,
    "Extra Carriage": ItemClassification.useful,
    "Budget Increase": ItemClassification.useful,
    "Clear Station": ItemClassification.useful,
}

TRAPS = [
    "Rush Hour",
    "Renovation",
    "Delayed",
    "Derailed",
]

class StartingMaps(Range):
    """Number of maps to start with unlocked"""
    display_name = "Starting Maps"
    range_start = 1
    range_end = 34
    default = 5


class MapsToComplete(Range):
    """Number of maps to complete for victory"""
    display_name = "Maps to Complete"
    range_start = 1
    range_end = 34
    default = 20


class TargetWeek(Range):
    """Target week level to complete"""
    display_name = "Target Week"
    range_start = 3
    range_end = 10
    default = 9


class MaxWeeks(Range):
    """Maximum number of weeks per map"""
    display_name = "Max Weeks"
    range_start = 3
    range_end = 10
    default = 9


class GameMode(Choice):
    """Game mode to play:
        Classic: Standard Mini Metro rules.
        Extreme: Lines cannot be redrawn after being placed.
    """
    display_name = "Game Mode"
    option_classic = 0
    option_extreme = 1
    default = 0


class TrapChance(Range):
    """Percentage of filler items replaced with traps:
        Rush Hour: A station gets overcrowded.
        Renovation: Your lines get removed and you must rebuild them from scratch.
        Delayed: Your next week is delayed by another week.
        Derailed: One of your trains gets permanently removed.
    """
    display_name = "Trap Chance"
    range_start = 0
    range_end = 100
    default = 20

class MapShardMode(Choice):
    """How many shards the player must collect to unlock each map.
        Static: pre-calculated from Max Weeks (max(max_weeks - 1 if max_weeks < 5 else max_weeks - 2, 1)).
        Custom: use the value of Custom Shard Count below."""
    display_name = "Map Shard Count Mode"
    option_static = 0
    option_custom = 1
    default = 0


class CustomShardCount(Range):
    """Shards required per map when Map Shard Count Mode is set to Custom. Ignored
    in Static mode. Higher values mean longer per-map gating between unlocks."""
    display_name = "Custom Shard Count"
    range_start = 1
    range_end = 10
    default = 5


@dataclass
class MiniMetroOptions(PerGameCommonOptions):
    """Options for Mini Metro world"""
    starting_maps: StartingMaps
    maps_to_complete: MapsToComplete
    target_week: TargetWeek
    max_weeks: MaxWeeks
    game_mode: GameMode
    trap_chance: TrapChance
    map_shard_mode: MapShardMode
    custom_shard_count: CustomShardCount
    death_link: DeathLink


class MiniMetroItem(Item):
    """Mini Metro Item"""
    game = "Mini Metro"


class MiniMetroLocation(Location):
    """Mini Metro Location"""
    game = "Mini Metro"


class MiniMetroWorld(World):
    """Mini Metro World"""
    game = "Mini Metro"
    topology_present = True
    options_dataclass = MiniMetroOptions
    item_name_to_id = _ITEM_NAME_TO_ID
    location_name_to_id = _LOCATION_NAME_TO_ID

    def __init__(self, multiworld: MultiWorld, player: int):
        super().__init__(multiworld, player)
        self.starting_maps: List[str] = []
        self.unlockable_maps: List[str] = []

    @classmethod
    def stage_assert_generate(cls, multiworld: MultiWorld):
        """Assert that the world is valid before generation"""
        pass

    def generate_early(self):
        """Early generation step"""
        num_starting = min(int(self.options.starting_maps.value), len(MAPS))
        maps_list = list(MAPS.keys())
        self.random.shuffle(maps_list)
        self.starting_maps = maps_list[:num_starting]
        self.unlockable_maps = maps_list[num_starting:]

    def resolved_shards_per_map(self) -> int:
        """determines the shard count based on the selected mode."""
        mw = int(self.options.max_weeks.value)
        if int(self.options.map_shard_mode.value) == 1:
            return min(int(self.options.custom_shard_count.value), mw - 1)
        return max(mw - 1 if mw < 5 else mw - 2, 1)
    
    def create_regions(self):
        """Create regions for Mini Metro - one per available map"""
        menu_region = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu_region)
        max_weeks = int(self.options.max_weeks.value)
        regions = {}
        available_maps = self.starting_maps + self.unlockable_maps
        for map_name in available_maps:
            region = Region(map_name, self.player, self.multiworld)
            regions[map_name] = region
            self.multiworld.regions.append(region)            
            for week in range(1, max_weeks + 1):
                location_name = f"{map_name} - Week {week}"
                if location_name in self.location_name_to_id:
                    location = MiniMetroLocation(
                        self.player,
                        location_name,
                        self.location_name_to_id[location_name],
                        region
                    )
                    region.locations.append(location)        
        for map_name in self.starting_maps:
            entrance = Entrance(self.player, f"Access {map_name}", menu_region)
            menu_region.exits.append(entrance)
            entrance.connect(regions[map_name])        
        for map_name in self.unlockable_maps:
            entrance = Entrance(self.player, f"Access {map_name}", menu_region)
            menu_region.exits.append(entrance)
            entrance.connect(regions[map_name])
    
    def create_items(self):
        """Create items for Mini Metro"""
        items_to_create = []        
        for _ in range(3):
            for item_name, classification in ITEMS.items():
                items_to_create.append(MiniMetroItem(
                    item_name,
                    classification,
                    self.item_name_to_id[item_name],
                    self.player
                ))   
        for _ in range(6):
            items_to_create.append(MiniMetroItem(
                "New Line - Unlock",
                ItemClassification.progression,
                self.item_name_to_id["New Line - Unlock"],
                self.player
            ))    
        max_weeks = int(self.options.max_weeks.value)
        shards_per_map = self.resolved_shards_per_map()
        progressive_shard_count = len(self.unlockable_maps) * shards_per_map
        for _ in range(progressive_shard_count):
            items_to_create.append(MiniMetroItem(
                "Progressive Map Shard",
                ItemClassification.progression,
                self.item_name_to_id["Progressive Map Shard"],
                self.player
            ))
        available_maps = self.starting_maps + self.unlockable_maps
        total_locations = len(available_maps) * max_weeks
        remaining_slots = total_locations - len(items_to_create)
        trap_chance = int(self.options.trap_chance.value)
        num_traps = (remaining_slots * trap_chance) // 100
        num_filler = remaining_slots - num_traps
        useful_item_names = ["Extra Locomotive", "Extra Carriage", "Budget Increase", "Clear Station"]
        for i in range(num_filler):
            useful_item = useful_item_names[i % len(useful_item_names)]
            items_to_create.append(MiniMetroItem(
                useful_item,
                ItemClassification.useful,
                self.item_name_to_id[useful_item],
                self.player
            ))
        for i in range(num_traps):
            trap_name = TRAPS[i % len(TRAPS)]
            items_to_create.append(MiniMetroItem(
                trap_name,
                ItemClassification.trap,
                self.item_name_to_id[trap_name],
                self.player
            ))
        self.multiworld.itempool += items_to_create
    
    def set_rules(self):
        """Set access rules for locations"""
        available_maps = self.starting_maps + self.unlockable_maps
        max_weeks = int(self.options.max_weeks.value)
        for map_name in available_maps:
            for week in range(2, max_weeks + 1):
                prev_location = f"{map_name} - Week {week - 1}"
                curr_location = f"{map_name} - Week {week}"
                if curr_location in self.location_name_to_id:
                    set_rule(
                        self.multiworld.get_location(curr_location, self.player),
                        lambda state, prev_loc=prev_location: state.can_reach_location(prev_loc, self.player)
                    )        
        shards_per_map = self.resolved_shards_per_map()
        for idx, map_name in enumerate(self.unlockable_maps):
            required = (idx + 1) * shards_per_map
            for week in range(1, max_weeks + 1):
                location_name = f"{map_name} - Week {week}"
                if location_name in self.location_name_to_id:
                    location = self.multiworld.get_location(location_name, self.player)
                    set_rule(
                        location,
                        lambda state, count=required: state.has("Progressive Map Shard", self.player, count)
                    )
        maps_to_beat = int(self.options.maps_to_complete.value)
        target_week = int(self.options.target_week.value)
        victory_locations = []
        available_maps = self.starting_maps + self.unlockable_maps
        for map_name in available_maps[:maps_to_beat]:
            location_name = f"{map_name} - Week {target_week}"
            if location_name in self.location_name_to_id:
                victory_locations.append(location_name)
        if victory_locations:
            self.multiworld.completion_condition[self.player] = lambda state: sum(
                1 for loc in victory_locations
                if state.can_reach_location(loc, self.player)
            ) >= maps_to_beat
    
    def fill_slot_data(self):
        """Return slot data"""
        return {
            "maps": MAPS,
            "starting_maps": self.starting_maps,
            "unlockable_maps": self.unlockable_maps,
            "maps_to_complete": int(self.options.maps_to_complete.value),
            "target_week": int(self.options.target_week.value),
            "max_weeks": int(self.options.max_weeks.value),
            "game_mode": "extreme" if int(self.options.game_mode.value) == 1 else "classic",
            "shards_per_map": self.resolved_shards_per_map(),
            "death_link": bool(self.options.death_link.value),
        }
