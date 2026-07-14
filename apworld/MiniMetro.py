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

_MAP_ID_TO_NAME = {v: k for k, v in MAPS.items()}
def _map_option_name(name: str) -> str:
    """Turn a display map name into a valid Choice ``option_*`` identifier."""
    cleaned = name.lower().replace(",", "").replace(".", "").replace("'", "")
    return "option_" + "_".join(cleaned.split())

ITEMS = {
    "Interchange - Unlock": ItemClassification.progression,
    "Shinkansen - Unlock": ItemClassification.progression,
    "Tunnel/Bridge - Unlock": ItemClassification.progression,
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

class Goal(Choice):
    """Victory condition:
        Complete Maps: clear Target Week on the configured number of maps (Maps to Complete).
        Goal Map: clear Target Week on a single chosen map (Goal Map below).
    """
    display_name = "Goal"
    option_complete_maps = 0
    option_goal_map = 1
    default = 0


GoalMap = type(Choice)("GoalMap", (Choice,), {
    "__doc__": "The map whose Target Week must be cleared to win when Goal is set to "
               "Goal Map. Ignored for the Complete Maps goal.",
    "display_name": "Goal Map",
    "default": MAPS["London"],
    **{_map_option_name(_name): _id for _name, _id in MAPS.items()},
})


class MapsToComplete(Range):
    """Number of maps to complete for victory when Goal is set to Complete Maps.
        Ignored when Goal is set to Goal Map."""
    display_name = "Maps to Complete"
    range_start = 1
    range_end = 34
    default = 20

class NumberOfMaps(Range):
    """Number of maps to include in the world."""
    display_name = "Number of Maps"
    range_start = 1
    range_end = 34
    default = 34


class TargetWeek(Range):
    """Target week level to complete."""
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
    number_of_maps: NumberOfMaps
    target_week: TargetWeek
    max_weeks: MaxWeeks
    game_mode: GameMode
    trap_chance: TrapChance
    map_shard_mode: MapShardMode
    custom_shard_count: CustomShardCount
    goal: Goal
    goal_map: GoalMap
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
        self.number_of_maps: int = len(MAPS)
        self.map_regions = {}

    @classmethod
    def stage_assert_generate(cls, multiworld: MultiWorld):
        """Assert that the world is valid before generation"""
        pass

    def _ut_passthrough(self):
        """Universal Tracker: the slot data UT handed back via interpret_slot_data,
        or None during a normal generation. Present only while UT re-generates the
        world to sync with a live seed."""
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        if isinstance(passthrough, dict):
            return passthrough.get(self.game)
        return None

    def interpret_slot_data(self, slot_data: dict) -> dict:
        """Universal Tracker hook. Returning the slot data (a truthy value) tells UT
        to re-run generation with this dict exposed via
        ``multiworld.re_gen_passthrough[self.game]`` so ``generate_early`` can restore
        the exact randomized map split for this seed instead of re-shuffling."""
        return slot_data

    def generate_early(self):
        """Early generation step"""
        # Under Universal Tracker, restore the randomized map split from slot data so
        # tracker logic matches the real seed; re-shuffling here would pick a
        # different set/order and mis-report which locations are in logic.
        pt = self._ut_passthrough()
        if pt:
            self.number_of_maps = int(pt["number_of_maps"])
            self.starting_maps = list(pt["starting_maps"])
            self.unlockable_maps = list(pt["unlockable_maps"])
        else:
            self.number_of_maps = min(int(self.options.number_of_maps.value), len(MAPS))
            maps_list = list(MAPS.keys())
            self.random.shuffle(maps_list)
            maps_list = maps_list[:self.number_of_maps]
            num_starting = min(int(self.options.starting_maps.value), self.number_of_maps)
            self.starting_maps = maps_list[:num_starting]
            self.unlockable_maps = maps_list[num_starting:]
        self.multiworld.early_items[self.player]["Tunnel/Bridge - Unlock"] = 1

    def resolved_shards_per_map(self) -> int:
        """determines the shard count based on the selected mode."""
        mw = int(self.options.max_weeks.value)
        if int(self.options.map_shard_mode.value) == 1:
            return min(int(self.options.custom_shard_count.value), mw - 1)
        return max(mw - 1 if mw < 5 else mw - 2, 1)
    
    def create_regions(self):
        """Create regions for Mini Metro - early/late split per available map"""
        menu_region = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu_region)
        max_weeks = int(self.options.max_weeks.value)
        available_maps = self.starting_maps + self.unlockable_maps
        has_late = max_weeks > 5
        for map_name in available_maps:
            early_region = Region(f"{map_name} - Early", self.player, self.multiworld)
            self.multiworld.regions.append(early_region)
            late_region = None
            if has_late:
                late_region = Region(f"{map_name} - Late", self.player, self.multiworld)
                self.multiworld.regions.append(late_region)
            self.map_regions[map_name] = (early_region, late_region)
            for week in range(1, max_weeks + 1):
                location_name = f"{map_name} - Week {week}"
                if location_name in self.location_name_to_id:
                    target_region = early_region if week <= 5 else late_region
                    location = MiniMetroLocation(
                        self.player,
                        location_name,
                        self.location_name_to_id[location_name],
                        target_region
                    )
                    target_region.locations.append(location)
        for map_name in available_maps:
            early_region, late_region = self.map_regions[map_name]
            entrance = Entrance(self.player, f"Access {map_name}", menu_region)
            menu_region.exits.append(entrance)
            entrance.connect(early_region)
            if late_region is not None:
                late_entrance = Entrance(self.player, f"Access {map_name} - Late", early_region)
                early_region.exits.append(late_entrance)
                late_entrance.connect(late_region)
    
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
            _, late_region = self.map_regions[map_name]
            if late_region is None:
                continue
            gate_location = f"{map_name} - Week 5"
            if gate_location in self.location_name_to_id:
                set_rule(
                    self.multiworld.get_entrance(f"Access {map_name} - Late", self.player),
                    lambda state, loc=gate_location: state.can_reach_location(loc, self.player)
                )
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
        target_week = int(self.options.target_week.value)
        goal_week = min(target_week, max_weeks)
        available_maps = self.starting_maps + self.unlockable_maps
        if int(self.options.goal.value) == 1:
            goal_map = _MAP_ID_TO_NAME[int(self.options.goal_map.value)]
            goal_location = f"{goal_map} - Week {goal_week}"
            if goal_location in self.location_name_to_id:
                self.multiworld.completion_condition[self.player] = \
                    lambda state, loc=goal_location: state.can_reach_location(loc, self.player)
        else:
            maps_to_beat = int(self.options.maps_to_complete.value)
            victory_locations = []
            for map_name in available_maps[:maps_to_beat]:
                location_name = f"{map_name} - Week {goal_week}"
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
            "number_of_maps": self.number_of_maps,
            "goal": int(self.options.goal.value),
            "goal_map": _MAP_ID_TO_NAME[int(self.options.goal_map.value)],
            "maps_to_complete": int(self.options.maps_to_complete.value),
            "target_week": int(self.options.target_week.value),
            "max_weeks": int(self.options.max_weeks.value),
            "game_mode": "extreme" if int(self.options.game_mode.value) == 1 else "classic",
            "shards_per_map": self.resolved_shards_per_map(),
            "death_link": bool(self.options.death_link.value),
        }
