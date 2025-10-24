#!/usr/bin/env python3

import sys
import re
import os

# Try to import PdfPig equivalent (PyPDF2 or pdfplumber)
try:
    import pdfplumber
    USE_PDFPLUMBER = True
except ImportError:
    USE_PDFPLUMBER = False
    try:
        import PyPDF2
    except ImportError:
        print("Installing required PDF library...")
        os.system("pip3 install pdfplumber")
        import pdfplumber
        USE_PDFPLUMBER = True

def extract_pete_sessions_occurrences(pdf_path):
    """Extract all occurrences of Pete Sessions from the PDF."""

    pete_sessions_occurrences = []
    all_text = []  # Store all text for debugging

    if USE_PDFPLUMBER:
        with pdfplumber.open(pdf_path) as pdf:
            for page_num, page in enumerate(pdf.pages, 1):
                text = page.extract_text()
                if text:
                    all_text.append(f"=== PAGE {page_num} ===\n{text}")
                    lines = text.split('\n')
                    for line_num, line in enumerate(lines, 1):
                        # Search for Sessions in various formats
                        if 'Sessions' in line and ('Pete' in line or 'TX' in line):
                            # Try to identify the context (committee or subcommittee)
                            context_lines = []
                            # Look at previous 20 lines for committee context
                            for i in range(max(0, line_num - 20), line_num):
                                if i < len(lines):
                                    prev_line = lines[i].strip()
                                    # Look for all-caps committee names
                                    if prev_line and prev_line.isupper() and len(prev_line) > 3 and not prev_line.startswith('SUBCOMMITTEE'):
                                        context_lines.append(prev_line)

                            pete_sessions_occurrences.append({
                                'page': page_num,
                                'line': line.strip(),
                                'context': context_lines[-1] if context_lines else 'Unknown'
                            })

    # If no occurrences found, let's search more broadly
    if not pete_sessions_occurrences:
        print("\nNo direct matches found. Searching for 'Sessions' in all text...")
        for page_text in all_text:
            lines = page_text.split('\n')
            for line in lines:
                if 'Sessions' in line:
                    print(f"  Found: {line.strip()}")

    return pete_sessions_occurrences

def main():
    pdf_path = '/Users/Luis/dev/congress-stock-trades/examples/scsoal.pdf'

    print("Analyzing Pete Sessions occurrences in SCSOAL PDF...")
    print("=" * 80)

    occurrences = extract_pete_sessions_occurrences(pdf_path)

    print(f"\nFound {len(occurrences)} occurrences of Pete Sessions:")
    print("-" * 80)

    for occ in occurrences:
        print(f"\nPage {occ['page']}:")
        print(f"  Line: {occ['line']}")
        print(f"  Context: {occ['context']}")

    # Group by context to understand committee assignments
    print("\n" + "=" * 80)
    print("Summary of Pete Sessions' committee assignments:")
    print("-" * 80)

    committees = {}
    for occ in occurrences:
        ctx = occ['context']
        if ctx not in committees:
            committees[ctx] = []
        committees[ctx].append(occ['line'])

    for committee, lines in committees.items():
        print(f"\n{committee}:")
        for line in lines:
            print(f"  - {line}")

if __name__ == "__main__":
    main()