# CITY LAB v1.5 - Entry point
# Run: python main.py

import tkinter as tk

from model import CityModel
from ui import CityUI


def main():
    root = tk.Tk()
    model = CityModel(40, 40, 6)
    CityUI(root, model)
    root.mainloop()


if __name__ == "__main__":
    main()
