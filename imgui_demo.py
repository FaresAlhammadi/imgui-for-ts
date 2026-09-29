"""
imgui_demo.py - the official Dear ImGui demo window (ImGui::ShowDemoWindow) on your PC.

    pip install imgui-bundle
    python imgui_demo.py

imgui-bundle is Dear ImGui compiled from github.com/ocornut/imgui with Python bindings and a
GLFW + OpenGL window. Nothing is restyled here: default dark theme, default ProggyClean font,
and the same demo window as imgui_demo.cpp in the GitHub repo.
"""
from imgui_bundle import hello_imgui, imgui


def load_fonts():
    imgui.get_io().fonts.add_font_default_bitmap()      # ProggyClean, Dear ImGui's default font


def setup_style():
    imgui.style_colors_dark()                           # Dear ImGui's default theme


def gui():
    imgui.show_demo_window()


def main():
    params = hello_imgui.RunnerParams()
    params.app_window_params.window_title = "Dear ImGui " + imgui.get_version()
    params.app_window_params.window_geometry.size = (1280, 800)
    params.imgui_window_params.default_imgui_window_type = hello_imgui.DefaultImGuiWindowType.no_default_window
    params.callbacks.load_additional_fonts = load_fonts
    params.callbacks.setup_imgui_style = setup_style
    params.callbacks.show_gui = gui
    params.ini_disable = True
    hello_imgui.run(params)


if __name__ == "__main__":
    main()
