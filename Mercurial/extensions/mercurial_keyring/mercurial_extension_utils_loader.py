
import os, sys

THE_DIR = os.path.dirname(os.path.abspath(__file__))

sys.path.append(THE_DIR)

# This makes this dir a winner (but later)
#
# def extsetup(ui):
#     sys.path.insert(0, THE_DIR)
