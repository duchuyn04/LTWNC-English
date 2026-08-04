from __future__ import annotations

import zipfile
from pathlib import Path


SOURCE = Path(r"C:\Users\juven\Downloads\NHOM 07_XAY DUNG WEBSITE HO TRO HOC TIENG ANH.docx")
GENERATED = Path(r"C:\it\ltwnc\NHOM 07_XAY DUNG WEBSITE HO TRO HOC TIENG ANH_bo_sung_mau.docx")
TEMP = GENERATED.with_suffix(".package-patched.docx")


def main() -> None:
    replace_parts = {"word/document.xml", "word/settings.xml"}
    with zipfile.ZipFile(SOURCE, "r") as source, zipfile.ZipFile(GENERATED, "r") as generated:
        with zipfile.ZipFile(TEMP, "w") as output:
            for info in source.infolist():
                data = generated.read(info.filename) if info.filename in replace_parts else source.read(info.filename)
                output.writestr(info, data)
    TEMP.replace(GENERATED)
    print(GENERATED)


if __name__ == "__main__":
    main()
