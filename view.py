import time

import numpy as np
from scipy.spatial import KDTree

import metrics

try:
    import pyvista as pv
except ImportError:
    pv = None


class Viewer3D:
    """Interactive PyVista viewer for the ureteroscopy path-planning demo."""

    def __init__(
        self,
        volume,
        caminho,
        pedra_xyz,
        inicio_xyz,
        curva=None,
        screenshot_path="pyvista_view.png",
        off_screen=False,
    ):
        if pv is None:
            raise ImportError(
                "PyVista/VTK nao esta instalado. Instale com: python -m pip install pyvista vtk"
            )

        self.volume = volume
        self.caminho = caminho or []
        self.pedra_xyz = pedra_xyz
        self.inicio_xyz = inicio_xyz
        self.curva = curva
        self.curva_array = np.asarray(curva, dtype=float) if curva else None
        self.screenshot_path = screenshot_path

        self.plotter = pv.Plotter(off_screen=off_screen)
        self.actors = {}
        self.show_a_star = False
        self.show_smoothed = True
        self.show_points = False
        self.show_external = False
        self.is_animating = False

        self.relatorio = self.calcular_extrapolacao() if curva else None

        self.setup_volume()
        self.setup_paths()
        self.setup_landmarks()
        self.setup_ui()
        self.setup_camera()

    def calcular_extrapolacao(self):
        return metrics.verificar_extrapolacao(self.curva, self.volume, limiar_distancia=0.1)

    def setup_volume(self):
        volume_corrigido = np.transpose(self.volume.astype(float), (2, 1, 0))
        grid = pv.ImageData(dimensions=volume_corrigido.shape, spacing=(1, 1, 1), origin=(0, 0, 0))
        grid.point_data["values"] = volume_corrigido.flatten(order="F")
        contours = grid.contour([0.1])

        if contours.n_points > 0:
            mesh = contours.smooth(n_iter=11, relaxation_factor=0.1).fill_holes(100)
        else:
            mesh = contours

        self.actors["volume"] = self.plotter.add_mesh(
            mesh,
            opacity=0.35,
            color=(240, 30, 0),
            name="Volume",
            smooth_shading=True,
            show_edges=False,
            pbr=True,
            metallic=0.0,
            ambient=0.3,
            diffuse=0.7,
            specular=0.5,
        )

    def setup_paths(self):
        if self.caminho:
            caminho_array = np.asarray(self.caminho, dtype=float)
            self.actors["a_star"] = self.plotter.add_mesh(
                pv.Spline(caminho_array),
                color="lime",
                line_width=3,
                name="A_star",
            )
            self.actors["a_star"].SetVisibility(self.show_a_star)

        if self.curva_array is not None:
            self.actors["curva"] = self.plotter.add_mesh(
                pv.Spline(self.curva_array),
                color="dodgerblue",
                line_width=6,
                name="Smoothed",
            )
            self.actors["curva"].SetVisibility(self.show_smoothed)
            self.setup_curve_points()

        self.setup_extrapolation()

    def setup_curve_points(self):
        points_actor = self.plotter.add_mesh(
            pv.PolyData(self.curva_array),
            color="dodgerblue",
            point_size=6,
            render_points_as_spheres=True,
            name="Curve Points",
        )
        points_actor.SetVisibility(self.show_points)
        self.actors["points"] = points_actor

    def setup_extrapolation(self):
        if not self.relatorio:
            return

        external_actors = []
        if self.relatorio["indices_fora"]:
            outside_points = self.curva_array[self.relatorio["indices_fora"]]
            outside_actor = self.plotter.add_mesh(
                pv.PolyData(outside_points),
                color="red",
                point_size=12,
                render_points_as_spheres=True,
                label="Fora do volume",
            )
            external_actors.append(outside_actor)

            volume_points = np.argwhere(self.volume == 1)
            tree_volume = KDTree(volume_points)
            for point in outside_points:
                point_volume = np.array([point[2], point[1], point[0]])
                _, idx = tree_volume.query(point_volume)
                surface_point = volume_points[idx][::-1]
                external_actors.append(self.plotter.add_mesh(pv.Line(point, surface_point), color="yellow", line_width=2))

        text_actor = self.plotter.add_text(
            f"Extrapolacao: {len(self.relatorio['indices_fora'])} pontos fora",
            position="lower_right",
            color="black",
            font_size=10,
        )
        external_actors.append(text_actor)

        self.actors["external"] = external_actors
        for actor in external_actors:
            actor.SetVisibility(self.show_external)

    def setup_landmarks(self):
        self.actors["pedra"] = self.plotter.add_mesh(
            pv.Sphere(radius=2.5, center=self.pedra_xyz),
            color=(255, 190, 0),
            name="Calculo",
        )
        self.actors["inicio"] = self.plotter.add_mesh(
            pv.Sphere(radius=2.0, center=self.inicio_xyz),
            color="green",
            name="Inicio",
        )

    def setup_ui(self):
        help_text = (
            "Controles:\n"
            "A - Liga/desliga A*\n"
            "Z - Liga/desliga curva suavizada\n"
            "D - Liga/desliga pontos da curva\n"
            "C - Liga/desliga checagem externa\n"
            "M - Animar camera pela rota\n"
            "R - Resetar camera\n"
            "S - Salvar screenshot"
        )
        self.plotter.add_text(help_text, position="upper_left", color="black", font_size=9)
        self.plotter.add_key_event("a", self.toggle_a_star)
        self.plotter.add_key_event("z", self.toggle_smoothed)
        self.plotter.add_key_event("d", self.toggle_points)
        self.plotter.add_key_event("c", self.toggle_external)
        self.plotter.add_key_event("m", self.toggle_animation)
        self.plotter.add_key_event("r", self.reset_camera)
        self.plotter.add_key_event("s", self.save_screenshot)

    def setup_camera(self):
        points = self.curva_array if self.curva_array is not None else np.asarray(self.caminho, dtype=float)
        if points is None or len(points) == 0:
            self.plotter.view_isometric()
            self.plotter.reset_camera()
            return

        center = points.mean(axis=0)
        extent = np.ptp(points, axis=0)
        distance = max(float(np.max(extent)) * 1.9, 90.0)
        self.plotter.camera.position = (
            center[0] + distance,
            center[1] - distance,
            center[2] + distance * 0.55,
        )
        self.plotter.camera.focal_point = tuple(center)
        self.plotter.camera.up = (0, 0, 1)
        self.plotter.reset_camera()

    def toggle_a_star(self):
        self.show_a_star = not self.show_a_star
        if "a_star" in self.actors:
            self.actors["a_star"].SetVisibility(self.show_a_star)
        self.plotter.update()

    def toggle_smoothed(self):
        self.show_smoothed = not self.show_smoothed
        if "curva" in self.actors:
            self.actors["curva"].SetVisibility(self.show_smoothed)
        self.plotter.update()

    def toggle_points(self):
        self.show_points = not self.show_points
        if "points" in self.actors:
            self.actors["points"].SetVisibility(self.show_points)
        self.plotter.update()

    def toggle_external(self):
        self.show_external = not self.show_external
        if "external" in self.actors:
            for actor in self.actors["external"]:
                actor.SetVisibility(self.show_external)

        if self.relatorio:
            print("\n--- Relatorio de extrapolacao ---")
            print(f"Pontos totais: {self.relatorio['total_pontos']}")
            print(f"Pontos fora: {self.relatorio['pontos_fora']} ({self.relatorio['percentual_fora']:.2f}%)")
            print(f"Distancia maxima a superficie: {self.relatorio['distancia_maxima']:.2f} voxels")
            print(f"Distancia media: {self.relatorio['distancia_media']:.2f} voxels")

        self.plotter.update()

    def toggle_animation(self):
        self.is_animating = not self.is_animating
        if self.is_animating and self.curva_array is not None:
            self.plotter.add_text("Camera: ON", name="anim_status", position="upper_right", color="darkblue", font_size=10)
            self.run_animation()
        else:
            self.plotter.add_text("Camera: OFF", name="anim_status", position="upper_right", color="darkblue", font_size=10)
            self.reset_camera()

    def reset_camera(self):
        self.setup_camera()
        self.plotter.update()

    def save_screenshot(self):
        self.plotter.screenshot(self.screenshot_path)
        print(f"Screenshot salvo em: {self.screenshot_path}")

    def run_animation(self):
        start_time = time.time()
        duration = 15.0
        total_points = len(self.curva_array)

        while self.is_animating and (time.time() - start_time) < duration:
            t = min((time.time() - start_time) / duration, 1.0)
            idx = t * (total_points - 1)
            idx0 = int(np.floor(idx))
            idx1 = min(idx0 + 1, total_points - 1)
            alpha = idx - idx0

            current_pos = (1 - alpha) * self.curva_array[idx0] + alpha * self.curva_array[idx1]
            look_ahead = min(idx0 + 4, total_points - 1)
            focal_point = self.curva_array[look_ahead]
            direction = focal_point - current_pos
            norm = np.linalg.norm(direction)
            direction = direction / norm if norm > 1e-6 else np.array([1.0, 0.0, 0.0])
            camera_pos = current_pos - direction * 12 + np.array([0, 0, 6])

            self.plotter.camera.position = tuple(camera_pos)
            self.plotter.camera.focal_point = tuple(focal_point)
            self.plotter.camera.up = (0, 0, 1)
            self.plotter.update()
            time.sleep(0.03)

        self.is_animating = False
        self.plotter.add_text("Camera: OFF", name="anim_status", position="upper_right", color="darkblue", font_size=10)
        self.plotter.update()

    def show(self):
        self.plotter.show(title="Trajetoria 3D - PyVista")
