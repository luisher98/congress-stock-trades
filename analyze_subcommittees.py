#!/usr/bin/env python3

import pdfplumber

def analyze_subcommittees():
    pdf_path = '/Users/Luis/dev/congress-stock-trades/examples/scsoal.pdf'

    with pdfplumber.open(pdf_path) as pdf:
        # Check specific pages where Pete Sessions appears in subcommittees
        pages_to_check = [23, 24, 25, 39, 40, 41]

        for page_num in pages_to_check:
            page = pdf.pages[page_num - 1]  # 0-indexed
            text = page.extract_text()

            print(f"\n{'='*80}")
            print(f"PAGE {page_num}")
            print('='*80)

            lines = text.split('\n')

            # Look for committee/subcommittee headers and Pete Sessions
            for i, line in enumerate(lines):
                # Print committee/subcommittee headers
                if line.strip().isupper() and len(line.strip()) > 3:
                    print(f"HEADER: {line.strip()}")

                # Print lines containing Sessions
                if 'Sessions' in line:
                    print(f"SESSIONS LINE {i}: '{line.strip()}'")
                    # Show context
                    if i > 0:
                        print(f"  PREVIOUS: '{lines[i-1].strip()}'")
                    if i < len(lines) - 1:
                        print(f"  NEXT: '{lines[i+1].strip()}'")

if __name__ == "__main__":
    analyze_subcommittees()