"""
Mini Metro World for Archipelago
"""

from typing import Dict, Set, List
import random
from BaseClasses import World, Region, Entrance, Item, Location, ItemClassification
from Archipelago.worlds.generic.Rules import set_rule
from Options import Range

# Define all maps in Mini Metro
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

# Item definitions
ITEM_DEFINITIONS = {
    "Budget Increase": ItemClassification.progression,
    "New Line - Unlock": ItemClassification.progression,
    "Interchange - Unlock": ItemClassification.progression,
    "Shinkansen - Unlock": ItemClassification.progression,
    "Tunnel/Bridge - Unlock": ItemClassification.progression,
    "Extra Train": ItemClassification.useful,
    "Extra Carriage": ItemClassification.useful,
    "Extra Speed": ItemClassification.useful,
}

MAP_ITEM_DEFINITIONS = {
    "London - Unlock": ItemClassification.progression,
    "Paris - Unlock": ItemClassification.progression,
    "New York City - Unlock": ItemClassification.progression,
    "Warsaw - Unlock": ItemClassification.progression,
    "Lisbon - Unlock": ItemClassification.progression,
    "Tokyo - Unlock": ItemClassification.progression,
    "Chicago - Unlock": ItemClassification.progression,
    "Budapest - Unlock": ItemClassification.progression,
    "Berlin - Unlock": ItemClassification.progression,
    "Melbourne - Unlock": ItemClassification.progression,
    "Hong Kong - Unlock": ItemClassification.progression,
    "Barcelona - Unlock": ItemClassification.progression,
    "Osaka - Unlock": ItemClassification.progression,
    "Stockholm - Unlock": ItemClassification.progression,
    "Saint Petersburg - Unlock": ItemClassification.progression,
    "Boston - Unlock": ItemClassification.progression,
    "Montreal - Unlock": ItemClassification.progression,
    "San Francisco - Unlock": ItemClassification.progression,
    "Sao Paulo - Unlock": ItemClassification.progression,
    "Seoul - Unlock": ItemClassification.progression,
    "Santiago - Unlock": ItemClassification.progression,
    "Washington, D.C. - Unlock": ItemClassification.progression,
    "Tashkent - Unlock": ItemClassification.progression,
    "Singapore - Unlock": ItemClassification.progression,
    "Cairo - Unlock": ItemClassification.progression,
    "Istanbul - Unlock": ItemClassification.progression,
    "Shanghai - Unlock": ItemClassification.progression,
    "Guangzhou - Unlock": ItemClassification.progression,
    "Nanjing - Unlock": ItemClassification.progression,
    "Chongqing - Unlock": ItemClassification.progression,
    "Mumbai - Unlock": ItemClassification.progression,
    "Addis Ababa - Unlock": ItemClassification.progression,
    "Lagos - Unlock": ItemClassification.progression,
    "Auckland - Unlock": ItemClassification.progression,
}

# Location definitions - Week completion for each map
# Locations are now created dynamically based on available maps


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
    """Target week level to complete (1-3)"""
    display_name = "Target Week"
    range_start = 1
    range_end = 50
    default = 9


class MiniMetroOptions:
    """Options for Mini Metro world"""
    starting_maps: StartingMaps
    maps_to_complete: MapsToComplete
    target_week: TargetWeek


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
    item_name_to_id: Dict[str, int] = {}
    location_name_to_id: Dict[str, int] = {}
    
    def __init__(self, multiworld: "MultiWorld", player: int):
        super().__init__(multiworld, player)
        self.starting_maps: List[str] = []
        self.unlockable_maps: List[str] = []
    
    @classmethod
    def stage_assert_generate(cls, multiworld: "MultiWorld"):
        """Assert that the world is valid before generation"""
        pass
    
    def generate_early(self):
        """Early generation step"""
        num_starting = min(int(self.options.starting_maps.value), len(MAPS))
        maps_list = list(MAPS.keys())
        random.shuffle(maps_list)        
        self.starting_maps = maps_list[:num_starting]
        self.unlockable_maps = maps_list[num_starting:]
        item_id = 100000        
        for item_name in ITEM_DEFINITIONS.keys():
            self.item_name_to_id[item_name] = item_id
            item_id += 1        
        for map_name in self.unlockable_maps:
            unlock_item = f"{map_name} - Unlock"
            self.item_name_to_id[unlock_item] = item_id
            item_id += 1        
        location_id = 200000
        available_maps = self.starting_maps + self.unlockable_maps
        for map_name in available_maps:
            for week in range(1, 4):
                location_name = f"{map_name} - Week {week}"
                self.location_name_to_id[location_name] = location_id
                location_id += 1
    
    def create_regions(self):
        """Create regions for Mini Metro - one per available map"""
        menu_region = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu_region)        
        regions = {}
        available_maps = self.starting_maps + self.unlockable_maps
        for map_name in available_maps:
            region = Region(map_name, self.player, self.multiworld)
            regions[map_name] = region
            self.multiworld.regions.append(region)            
            for week in range(1, 4):
                location_name = f"{map_name} - Week {week}"
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
            for item_name, classification in ITEM_DEFINITIONS.items():
                items_to_create.append(MiniMetroItem(
                    item_name,
                    classification,
                    self.item_name_to_id[item_name],
                    self.player
                ))        
        for map_name in self.unlockable_maps:
            unlock_item_name = f"{map_name} - Unlock"
            items_to_create.append(MiniMetroItem(
                unlock_item_name,
                ItemClassification.progression,
                self.item_name_to_id[unlock_item_name],
                self.player
            ))
        self.multiworld.itempool += items_to_create
    
    def set_rules(self):
        """Set access rules for locations"""
        available_maps = self.starting_maps + self.unlockable_maps
        for map_name in available_maps:
            for week in range(2, 4):
                prev_location = f"{map_name} - Week {week - 1}"
                curr_location = f"{map_name} - Week {week}"
                set_rule(
                    self.multiworld.get_location(curr_location, self.player),
                    lambda state, prev_loc=prev_location: state.can_reach_location(prev_loc, self.player)
                )        
        for map_name in self.unlockable_maps:
            unlock_item_name = f"{map_name} - Unlock"
            for week in range(1, 4):
                location_name = f"{map_name} - Week {week}"
                location = self.multiworld.get_location(location_name, self.player)
                set_rule(
                    location,
                    lambda state, item_name=unlock_item_name: state.has(item_name, self.player)
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
            set_rule(
                self.multiworld.completion_condition[self.player],
                lambda state: sum(1 for loc in victory_locations 
                    if state.can_reach_location(loc, self.player)) >= maps_to_beat
            )
    
    def fill_slot_data(self):
        """Return slot data"""
        return {
            "maps": MAPS,
            "starting_maps": self.starting_maps,
            "unlockable_maps": self.unlockable_maps,
            "maps_to_complete": int(self.options.maps_to_complete.value),
            "target_week": int(self.options.target_week.value),
        }
