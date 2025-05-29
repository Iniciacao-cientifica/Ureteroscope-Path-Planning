import numpy as np
import pyvista as pv
import time
from scipy.spatial import KDTree
import metrics

class Viewer3D:
    def __init__(self, volume, caminho, pedra_xyz, inicio_xyz, curva=None):
        self.volume = volume
        self.caminho = caminho
        self.pedra_xyz = pedra_xyz
        self.inicio_xyz = inicio_xyz
        self.curva = curva
        self.curva_array = np.array(curva) if curva else None
        
        self.plotter = pv.Plotter()
        self.actors = {}
        
        # Estados de visibilidade
        self.show_a_star = False
        self.show_smoothed = True
        self.show_dotted = False
        self.show_external = False
        self.is_animating = False
        
        # Relatório de extrapolação
        self.relatorio = self.calcular_extrapolacao() if curva else None
        
        # Inicialização dos componentes
        self.setup_volume()
        self.setup_caminhos()
        self.setup_pontos_interesse()
        self.setup_ui()
        
    def calcular_extrapolacao(self):
        """Calcula os pontos fora do volume"""
        # Implemente sua função verificar_extrapolacao aqui
        relatorio = metrics.verificar_extrapolacao(self.curva, self.volume, limiar_distancia=0.1)
        return relatorio

    def setup_volume(self):
        """Configura a visualização do volume"""
        # 1. Suavização do volume
        volume_suavizado = self.volume.astype(float)
        
        
        volume_corrigido = np.transpose(volume_suavizado, (2, 1, 0))
        grid = pv.ImageData(
            dimensions=volume_corrigido.shape,
            spacing=(1, 1, 1),
            origin=(0, 0, 0)
        )
        grid.point_data["values"] = volume_corrigido.flatten(order="F")
        
        # 2. Extração de superfície
        contours = grid.contour([0.1])
        
        # 3. Suavização da malha
        if contours.n_points > 0:
            suavizado = contours.smooth(
                n_iter=11, 
                relaxation_factor=0.1
            )
            suavizado = suavizado.fill_holes(100)
        else:
            suavizado = contours
        
        # 4. Adição da malha
        self.plotter.add_mesh(
            suavizado, 
            opacity=0.7, 
            color=(240, 30, 0), 
            name='Volume',
            smooth_shading=True,
            show_edges=False,
            pbr=True,
            metallic=0.0,
            ambient=0.3,
            diffuse=0.7,
            specular=0.5
        )

    def setup_caminhos(self):
        """Configura os caminhos e pontos relacionados"""
        # Caminho A* original
        if self.caminho:
            caminho_array = np.array(self.caminho)
            self.actors['a_star'] = self.plotter.add_mesh(
                pv.Spline(caminho_array), 
                color='green', 
                line_width=3,
                name='A_star'
            )
            self.actors['a_star'].SetVisibility(self.show_a_star)

        # Curva Suavizada
        if self.curva_array is not None:
            self.actors['curva'] = self.plotter.add_mesh(
                pv.Spline(self.curva_array),
                color='blue',
                line_width=5,
                name='Smoothed'
            )
            self.actors['curva'].SetVisibility(self.show_smoothed)

        # Pontos fora do volume
        self.setup_extrapolacao()
        
        # Pontos e linhas tracejadas
        self.setup_pontos_linhas()

    def setup_extrapolacao(self):
        """Configura visualização dos pontos fora do volume"""
        if not self.relatorio:
            return
            
        pontos_actor = []
        
        # Pontos fora do volume
        if self.relatorio['indices_fora']:
            pontos_fora = self.curva_array[self.relatorio['indices_fora']]
            pontos_plot = pv.PolyData(pontos_fora)
            pontos_actor.append(self.plotter.add_mesh(
                pontos_plot, color='red', point_size=10,
                render_points_as_spheres=True, label='Fora do volume'
            ))
            
            # Conexões com o volume
            pontos_volume = np.argwhere(self.volume == 1)
            tree_volume = KDTree(pontos_volume)
            
            for p in pontos_fora:
                ponto_vol = np.array([p[2], p[1], p[0]])
                _, idx = tree_volume.query(ponto_vol)
                ponto_surface = pontos_volume[idx][::-1]
                linha = pv.Line(p, ponto_surface)
                pontos_actor.append(self.plotter.add_mesh(linha, color='yellow', line_width=2))
        
        # Texto informativo
        pontos_actor.append(self.plotter.add_text(
            f"Extrapolação: {len(self.relatorio['indices_fora'])} pontos fora", 
            position='lower_right', color='black'
        ))
        
        self.actors['external'] = pontos_actor
        for actor in self.actors['external']:
            actor.SetVisibility(self.show_external)

    def setup_pontos_linhas(self):
        """Configura pontos e linhas tracejadas ao longo da curva"""
        if self.curva_array is None:
            return
            
        # Pontos azuis ao longo da curva
        points_actor = self.plotter.add_mesh(
            pv.PolyData(self.curva_array),
            color='blue',
            point_size=5,
            render_points_as_spheres=True,
            name='Points'
        )
        
        # Linhas tracejadas
        dashed_points = []
        lines = []
        dash_length = 0.1
        gap_length = 0.05
        
        for i in range(len(self.curva_array)-1):
            start = self.curva_array[i]
            end = self.curva_array[i+1]
            direction = end - start
            length = np.linalg.norm(direction)
            direction /= length
            
            current_pos = 0.0
            while current_pos < length:
                dash_start = start + direction * current_pos
                current_pos += dash_length
                dash_end = start + direction * min(current_pos, length)
                
                idx = len(dashed_points)
                dashed_points.extend([dash_start, dash_end])
                lines.append([2, idx, idx+1])
                
                current_pos += gap_length
        
        if dashed_points:
            dashed_lines = pv.PolyData()
            dashed_lines.points = dashed_points
            dashed_lines.lines = lines
            
            lines_actor = self.plotter.add_mesh(
                dashed_lines,
                color='black',
                line_width=2,
                name='Dashed Lines'
            )
            self.actors['dotted'] = (points_actor, lines_actor)
            for actor in self.actors['dotted']:
                actor.SetVisibility(self.show_dotted)

    def setup_pontos_interesse(self):
        """Configura os pontos de interesse (pedra e início)"""
        self.actors['pedra'] = self.plotter.add_mesh(
            pv.Sphere(radius=2, center=self.pedra_xyz), 
            color=(150, 100, 0),
            name='Pedra'
        )
        
        self.actors['inicio'] = self.plotter.add_mesh(
            pv.Sphere(radius=1, center=self.inicio_xyz), 
            color='green',
            name='Inicio'
        )

    def setup_ui(self):
        """Configura a interface do usuário e callbacks"""
        # Texto de ajuda
        help_text = """Controles:
    A - Toggle A*
    Z - Toggle Suavizada
    D - Toggle Dotted
    C - Toggle External Check
    M - Toggle Animação"""
        
        self.plotter.add_text(help_text, position='upper_left', color='black', font_size=9)
        
        # Registro de eventos de teclado
        self.plotter.add_key_event('a', self.toggle_a_star)
        self.plotter.add_key_event('z', self.toggle_smoothed)
        self.plotter.add_key_event('d', self.toggle_dotted)
        self.plotter.add_key_event('c', self.toggle_external)
        self.plotter.add_key_event('m', self.toggle_animation)

    # Métodos de toggle
    def toggle_a_star(self):
        self.show_a_star = not self.show_a_star
        self.actors['a_star'].SetVisibility(self.show_a_star)
        self.plotter.update()

    def toggle_smoothed(self):
        self.show_smoothed = not self.show_smoothed
        self.actors['curva'].SetVisibility(self.show_smoothed)
        self.plotter.update()

    def toggle_dotted(self):
        self.show_dotted = not self.show_dotted
        if 'dotted' in self.actors:
            for actor in self.actors['dotted']:
                actor.SetVisibility(self.show_dotted)
        self.plotter.update()

    def toggle_external(self):
        self.show_external = not self.show_external
        if 'external' in self.actors:
            for actor in self.actors['external']:
                actor.SetVisibility(self.show_external)
        
        # Exibe relatório no console
        if self.relatorio:
            print("\n--- Relatório de Extrapolação ---")
            print(f"Pontos totais: {self.relatorio['total_pontos']}")
            print(f"Pontos fora: {self.relatorio['pontos_fora']} ({self.relatorio['percentual_fora']:.2f}%)")
            print(f"Distância máxima à superfície: {self.relatorio['distancia_maxima']:.2f} voxels")
            print(f"Distância média: {self.relatorio['distancia_media']:.2f} voxels")
            
            if self.relatorio['pontos_fora'] > 0:
                print("\nCoordenadas dos pontos fora:")
                print(self.relatorio['coordenadas_fora'])
        
        self.plotter.update()

    def toggle_animation(self):
        self.is_animating = not self.is_animating
        
        if self.is_animating and self.curva_array is not None:
            self.plotter.add_text("Camera: ON", name='anim_status',
                               position='upper_right', color='darkblue', font_size=10)
            self.executar_animacao()
        else:
            self.plotter.reset_camera()
            self.plotter.add_text("Camera: OFF", name='anim_status',
                               position='upper_right', color='darkblue', font_size=10)
            self.plotter.update()

    def executar_animacao(self):
        """Executa a animação da câmera ao longo da curva"""
        # Configura posição inicial
        self.plotter.camera_position = [
            self.curva_array[0],
            self.curva_array[1],
            (0, 0, 1)
        ]
        
        start_time = time.time()
        duration = 15
        total_points = len(self.curva_array)
        
        while self.is_animating and (time.time() - start_time) < duration:
            elapsed = time.time() - start_time
            t = elapsed / duration
            
            if t >= 1.0:
                break
                
            # Interpolação da posição
            idx = t * (total_points - 1)
            idx0 = int(np.floor(idx))
            idx1 = min(idx0 + 1, total_points - 1)
            alpha = idx - idx0
            
            current_pos = (1 - alpha) * self.curva_array[idx0] + alpha * self.curva_array[idx1]
            
            # Ponto de foco
            look_ahead = min(idx0 + 3, total_points - 1)
            focal_point = self.curva_array[look_ahead]
            
            # Atualiza câmera
            self.plotter.camera.position = current_pos
            self.plotter.camera.focal_point = focal_point
            self.plotter.camera.up = (0, 0, 1)
            
            self.plotter.update()
            time.sleep(0.03)
        
        # Finalização da animação
        self.is_animating = False
        self.plotter.reset_camera()
        self.plotter.add_text("Camera: OFF", name='anim_status',
                           position='upper_right', color='darkblue', font_size=10)
        self.plotter.update()

    def show(self):
        """Exibe a visualização 3D"""
        self.plotter.show(title='Trajetória 3D com Controles')

# Exemplo de uso:
# visualizador = Visualizador3D(volume, caminho, pedra_xyz, inicio_xyz, curva)
# visualizador.mostrar()