TextLayer ships Tesseract language data from the official tesseract-ocr tessdata_best project.

Required files for Accurate OCR:
- eng.traineddata
- rus.traineddata

At runtime these files are copied next to the app output under:
- tessdata\eng.traineddata
- tessdata\rus.traineddata

If you prepare a portable or release build manually, keep the tessdata folder beside TextLayer.exe.
