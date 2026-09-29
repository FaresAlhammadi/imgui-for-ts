"""
imgui_pc.py - a real Dear ImGui window on your PC, styled like the menu (purple theme,
ProggyClean font, connected tabs).

    pip install imgui-bundle
    python imgui_pc.py

imgui-bundle ships the actual Dear ImGui C++ library with Python bindings and its own window
(GLFW + OpenGL), so there's nothing else to install. Edit gui() to change what's in the menu.
"""
from imgui_bundle import hello_imgui, imgui
from imgui_bundle import ImVec2, ImVec4

TITLE = "astraeus debug imgui"
SCALE = 1.5          # everything (font, padding, rounding) scales with this

HANDS = ["Both", "Left", "Right"]
MODES = ["Normal", "Fast", "Chaos"]
ITEMS = [f"Placeholder Item {i:02d}" for i in range(1, 25)]


class State:
    toggles = [False] * 6
    speed = 1.0
    hand = 0
    mode = 0
    amount = 1
    item = 0
    search = ""
    volume = 1.0
    status = ""


S = State()


def load_fonts():
    # ProggyClean - Dear ImGui's built-in default font
    imgui.get_io().fonts.add_font_default_bitmap()


def setup_style():
    style = imgui.get_style()
    style.font_scale_main = SCALE
    style.window_rounding = 12
    style.frame_rounding = 6
    style.grab_rounding = 6
    style.tab_rounding = 9
    style.popup_rounding = 6
    style.scrollbar_rounding = 6
    style.child_rounding = 6
    style.window_border_size = 1
    style.frame_padding = ImVec2(10, 6)
    style.item_spacing = ImVec2(8, 6)
    style.window_padding = ImVec2(12, 10)
    style.scale_all_sizes(SCALE)

    c = imgui.Col_
    colors = {
        c.text: (0.92, 0.91, 0.95, 1.00),
        c.text_disabled: (0.60, 0.57, 0.68, 1.00),
        c.window_bg: (0.035, 0.028, 0.055, 0.94),
        c.popup_bg: (0.10, 0.08, 0.15, 0.97),
        c.border: (0.46, 0.41, 0.62, 0.55),
        c.frame_bg: (0.36, 0.31, 0.48, 0.62),
        c.frame_bg_hovered: (0.45, 0.39, 0.60, 0.72),
        c.frame_bg_active: (0.53, 0.47, 0.70, 0.80),
        c.title_bg: (0.06, 0.05, 0.09, 1.00),
        c.title_bg_active: (0.10, 0.08, 0.15, 1.00),
        c.title_bg_collapsed: (0.06, 0.05, 0.09, 1.00),
        c.check_mark: (0.74, 0.62, 1.00, 1.00),
        c.slider_grab: (0.60, 0.55, 0.78, 1.00),
        c.slider_grab_active: (0.76, 0.70, 0.95, 1.00),
        c.button: (0.52, 0.48, 0.64, 0.85),
        c.button_hovered: (0.62, 0.57, 0.76, 0.92),
        c.button_active: (0.72, 0.66, 0.88, 1.00),
        c.header: (0.40, 0.34, 0.54, 0.66),
        c.header_hovered: (0.48, 0.42, 0.64, 0.80),
        c.header_active: (0.56, 0.50, 0.72, 0.90),
        c.separator: (0.46, 0.41, 0.62, 0.50),
        c.tab: (0.36, 0.31, 0.48, 0.72),
        c.tab_hovered: (0.50, 0.44, 0.66, 0.85),
        c.tab_selected: (0.56, 0.50, 0.70, 0.90),
        c.tab_dimmed: (0.36, 0.31, 0.48, 0.72),
        c.tab_dimmed_selected: (0.56, 0.50, 0.70, 0.90),
        c.scrollbar_bg: (0.05, 0.04, 0.08, 0.60),
        c.scrollbar_grab: (0.46, 0.41, 0.62, 0.80),
        c.plot_histogram: (0.74, 0.62, 1.00, 0.90),
        c.resize_grip: (0.56, 0.50, 0.72, 0.35),
        c.resize_grip_hovered: (0.62, 0.57, 0.80, 0.70),
        c.resize_grip_active: (0.72, 0.66, 0.90, 0.95),
    }
    for idx, rgba in colors.items():
        style.set_color_(idx, ImVec4(*rgba))


def placeholder_button(label):
    if imgui.button(label):
        S.status = f"{label} clicked (placeholder - no script attached)"


def tab_player():
    if imgui.collapsing_header("Movement", imgui.TreeNodeFlags_.default_open):
        _, S.toggles[0] = imgui.checkbox("Placeholder Toggle 1", S.toggles[0])
        _, S.toggles[1] = imgui.checkbox("Placeholder Toggle 2", S.toggles[1])
        _, S.speed = imgui.slider_float("Placeholder Speed", S.speed, 1.0, 10.0, "%.2f")
        if imgui.tree_node("Placeholder Group"):
            _, S.hand = imgui.combo("Placeholder Hand", S.hand, HANDS)
            _, S.toggles[2] = imgui.checkbox("Placeholder Toggle 3", S.toggles[2])
            _, S.toggles[3] = imgui.checkbox("Placeholder Toggle 4", S.toggles[3])
            imgui.tree_pop()
    if imgui.collapsing_header("Actions", imgui.TreeNodeFlags_.default_open):
        placeholder_button("Placeholder Button 1")
        imgui.same_line()
        placeholder_button("Placeholder Button 2")


def tab_items():
    _, S.search = imgui.input_text("Search", S.search)
    shown = [i for i in ITEMS if S.search.lower() in i.lower()]
    if shown:
        cur = shown.index(ITEMS[S.item]) if ITEMS[S.item] in shown else 0
        changed, cur = imgui.list_box("##items", cur, shown, 8)
        if changed:
            S.item = ITEMS.index(shown[cur])
    else:
        imgui.text_disabled("no matches")
    imgui.text(f"Selected: {ITEMS[S.item]}")
    placeholder_button("Use Selected Item")


def tab_spawning():
    _, S.mode = imgui.combo("Placeholder Mode", S.mode, MODES)
    _, S.amount = imgui.slider_int("Placeholder Amount", S.amount, 1, 20)
    _, S.toggles[5] = imgui.checkbox("Placeholder Toggle 6", S.toggles[5])
    placeholder_button("Spawn Placeholder")
    imgui.same_line()
    placeholder_button("Clear Placeholders")


def tab_settings():
    _, S.volume = imgui.slider_float("Volume", S.volume, 0.0, 1.0, "%.2f")
    imgui.progress_bar(S.volume, ImVec2(-1, 0), f"{int(S.volume * 100)}%")
    imgui.separator_text("About")
    imgui.text_colored(ImVec4(0.80, 0.70, 1.0, 1.0), TITLE)
    imgui.text_disabled(f"Dear ImGui {imgui.get_version()} - running on your PC")


def gui():
    viewport = imgui.get_main_viewport()
    imgui.set_next_window_pos(viewport.work_pos, imgui.Cond_.always)
    imgui.set_next_window_size(viewport.work_size, imgui.Cond_.always)
    flags = imgui.WindowFlags_.no_title_bar | imgui.WindowFlags_.no_move | imgui.WindowFlags_.no_resize | imgui.WindowFlags_.no_collapse
    imgui.begin(TITLE, None, flags)
    imgui.text(TITLE)
    imgui.text(f"FPS: {imgui.get_io().framerate:.1f}")
    imgui.spacing()
    if imgui.begin_tab_bar("tabs", imgui.TabBarFlags_.fitting_policy_scroll):
        for name, draw in (("Player", tab_player), ("Items", tab_items),
                           ("Spawning", tab_spawning), ("Settings", tab_settings)):
            if imgui.begin_tab_item(name)[0]:
                imgui.spacing()
                draw()
                imgui.end_tab_item()
        imgui.end_tab_bar()
    if S.status:
        imgui.separator()
        imgui.text_disabled(S.status)
    imgui.end()


def main():
    params = hello_imgui.RunnerParams()
    params.app_window_params.window_title = TITLE
    params.app_window_params.window_geometry.size = (1118, 760)
    params.imgui_window_params.default_imgui_window_type = hello_imgui.DefaultImGuiWindowType.no_default_window
    params.imgui_window_params.background_color = ImVec4(0.02, 0.015, 0.03, 1.0)
    params.callbacks.load_additional_fonts = load_fonts
    params.callbacks.setup_imgui_style = setup_style
    params.callbacks.show_gui = gui
    params.fps_idling.enable_idling = True     # near-zero CPU when you're not touching it
    hello_imgui.run(params)


if __name__ == "__main__":
    main()
