"""
Mini Metro World for Archipelago
"""

from typing import Dict, Set, List
from BaseClasses import World, Region, Entrance, Item, Location, ItemClassification
from Archipelago.worlds.generic.Rules import set_rule
from Archipelago.worlds.AutoWorld import World as WorldType


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
    items: Dict[str, int] = {}
    locations: Dict[str, int] = {}
    
    def __init__(self, multiworld: "MultiWorld", player: int):
        super().__init__(multiworld, player)
    
    @classmethod
    def stage_assert_generate(cls, multiworld: "MultiWorld"):
        """Assert that the world is valid before generation"""
        pass
    
    def generate_early(self):
        """Early generation step"""
        pass
    
    def create_regions(self):
        """Create regions for Mini Metro"""
        menu_region = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu_region)
    
    def create_items(self):
        """Create items for Mini Metro"""
        pass
    
    def set_rules(self):
        """Set access rules for locations"""
        pass
    
    def fill_slot_data(self):
        """Return slot data"""
        return {}
