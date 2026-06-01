import time
import random
import math
import win32gui
import win32con
import win32api

# ==================== НАСТРОЙКИ ====================
# Твой список координат категорий меню
BUTTONS = [
    (133, 133), (153, 183), (156, 227), (130, 273), (136, 318),
    (114, 365), (120, 410), (133, 452), (121, 496), (150, 544), (170, 590)
]

# Границы поля объявлений (Левый верхний и Правый нижний углы)
AD_ZONE_X1, AD_ZONE_Y1 = 314, 125
AD_ZONE_X2, AD_ZONE_Y2 = 1881, 988


# ===================================================


def find_gta_hwnd():
    target_titles = ["Grand Theft Auto V", "Majestic Multiplayer"]
    for title in target_titles:
        hwnd = win32gui.FindWindow(None, title)
        if hwnd:
            return hwnd, title

    found_windows = []

    def callback(hwnd, _):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if "Grand Theft Auto" in title or "Majestic" in title:
                found_windows.append((hwnd, title))
        return True

    win32gui.EnumWindows(callback, None)
    return found_windows[0] if found_windows else (None, None)


def move_and_click_background(hwnd, target_x, target_y):
    """Плавно ведет курсор из случайной точки и кликает в фоне"""
    start_x, start_y = random.randint(100, 1800), random.randint(100, 900)
    steps = random.randint(15, 25)

    for i in range(steps):
        t = math.sin((i / float(steps - 1)) * math.pi / 2)
        curr_x = int(start_x + (target_x - start_x) * t)
        curr_y = int(start_y + (target_y - start_y) * t)

        # Плавное движение
        win32gui.PostMessage(hwnd, win32con.WM_MOUSEMOVE, 0, win32api.MAKELONG(curr_x, curr_y))
        time.sleep(0.01)

    # Небольшой разброс пикселей в точке клика для беспалевности
    final_x = target_x + random.randint(-4, 4)
    final_y = target_y + random.randint(-4, 4)
    final_lparam = win32api.MAKELONG(final_x, final_y)

    win32gui.PostMessage(hwnd, win32con.WM_MOUSEMOVE, 0, final_lparam)
    time.sleep(0.05)

    # Клик ЛКМ
    win32gui.PostMessage(hwnd, win32con.WM_LBUTTONDOWN, win32con.MK_LBUTTON, final_lparam)
    time.sleep(random.uniform(0.07, 0.15))
    win32gui.PostMessage(hwnd, win32con.WM_LBUTTONUP, 0, final_lparam)


def send_escape_background(hwnd):
    """Посылает нажатие клавиши ESCAPE в фоновое окно"""
    # Отправляем сигнал "Клавиша зажата"
    win32gui.PostMessage(hwnd, win32con.WM_KEYDOWN, win32con.VK_ESCAPE, 0)
    # Держим кнопку как человек
    time.sleep(random.uniform(0.08, 0.14))
    # Отпускаем
    win32gui.PostMessage(hwnd, win32con.WM_KEYUP, win32con.VK_ESCAPE, 0)


def main():
    hwnd, title = find_gta_hwnd()
    if not hwnd:
        print("Окно не найдено!")
        return

    print(f"Успешно подключено к: {title}")
    print("[*] Анти-АФК «Покупатель» запущен. Можешь сворачивать окна поверх игры.")
    last_idx = -1

    while True:
        # Проверяем, не закрылась ли игра
        if not win32gui.IsWindow(hwnd):
            print("[-] Окно игры закрылось. Остановка скрипта.")
            break

        # --- ШАГ 1: Клик по кнопке категории ---
        idx = random.choice([i for i in range(len(BUTTONS)) if i != last_idx])
        last_idx = idx

        print(f"\n[*] Переключаемся на категорию меню №{idx + 1}...")
        move_and_click_background(hwnd, BUTTONS[idx][0], BUTTONS[idx][1])

        # --- ШАГ 2: Ждем 25-30 секунд ---
        wait_before_ad = random.uniform(25.0, 30.0)
        print(f"[~] Ждем {wait_before_ad:.2f} сек. перед выбором объявления...")
        time.sleep(wait_before_ad)

        # --- ШАГ 3: Клик по случайному объявлению в поле ---
        ad_x = random.randint(AD_ZONE_X1, AD_ZONE_X2)
        ad_y = random.randint(AD_ZONE_Y1, AD_ZONE_Y2)

        print(f"[*] Заходим в случайное объявление по координатам: ({ad_x}, {ad_y})...")
        move_and_click_background(hwnd, ad_x, ad_y)

        # --- ШАГ 4: Ждем 25-30 секунд внутри объявления ---
        wait_inside_ad = random.uniform(25.0, 30.0)
        print(f"[~] Просмотр объявления внутри. Ждем {wait_inside_ad:.2f} сек...")
        time.sleep(wait_inside_ad)

        # --- ШАГ 5: Выходим из объявления через Escape ---
        print("[*] Закрываем объявление нажатием Escape...")
        send_escape_background(hwnd)

        # --- ШАГ 6: Большой АФК-перерыв между циклами (3-6 минут) ---
        main_sleep = random.uniform(180.0, 360.0)
        print(f"[Zzz] Цикл завершен. Засыпаем на {round(main_sleep / 60, 2)} мин. до следующей категории...")
        time.sleep(main_sleep)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n[-] Скрипт остановлен пользователем.")