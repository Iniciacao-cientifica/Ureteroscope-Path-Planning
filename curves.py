
import numpy as np
from scipy.signal import savgol_filter
from pykalman import KalmanFilter
from scipy.interpolate import splprep, splev


def savgol_curve(path, window_ratio=0.15, iterations=3, order=5):
    """
    Suavização robusta com múltiplos métodos e tratamento de bordas
    """
    if len(path) < 4:
        return path
    
    curve = np.array(path)
    n_points = len(curve)
    
    # Configuração adaptativa da janela
    min_window = 5
    window = max(min_window, int(n_points * window_ratio))
    if window % 2 == 0:
        window += 1  # Savitzky-Golay requer janela ímpar
    
    for _ in range(iterations):
        try:
            x = savgol_filter(curve[:,0], window_length=window, polyorder=order, mode='interp')
            y = savgol_filter(curve[:,1], window_length=window, polyorder=order, mode='interp')
            z = savgol_filter(curve[:,2], window_length=window, polyorder=order, mode='interp')
        except:
            x, y, z = curve[:,0], curve[:,1], curve[:,2]

        curve = np.vstack([x, y, z]).T
    
    return curve.tolist()


def bspline_curve(points, degree=3, smooth_factor=None, n=100):
    """
    Gera curva B-spline com controle completo dos parâmetros
    :param points: Pontos de entrada
    :param degree: Grau da curva (default=3 cúbica)
    :param smoothness: Fator de suavização (None para interpolação)
    :param num_points: Número de pontos na curva final
    """
    points_np = np.array(points).T
    tck, u = splprep(points_np, k=degree, s=smooth_factor)
    curve = (np.array(splev(u, tck)).T).tolist()
    return curve

def kalman_curve(points, process_noise=0.1, measurement_noise=1.0):
    """Suavização com Filtro de Kalman"""
    kf = KalmanFilter(
        transition_matrices=np.eye(3),
        observation_matrices=np.eye(3),
        initial_state_mean=points[0],
        observation_covariance=measurement_noise*np.eye(3),
        transition_covariance=process_noise*np.eye(3)
    )
    curve, _ = kf.smooth(points)
    return curve.tolist()

def laplacian_curve(points, iterations=10, lambda_factor=0.5):
    """Suavização baseada em difusão laplaciana"""
    curve = np.asarray(points, dtype="float64")
    for _ in range(iterations):
        for i in range(1, len(points)-1):
            # Operador laplaciano: L = P_{i-1} + P_{i+1} - 2*P_i
            laplacian = (curve[i-1] + curve[i+1] - 2*curve[i])
            curve[i] += lambda_factor * laplacian
    return curve.tolist()
