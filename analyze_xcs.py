#!/usr/bin/env python3
"""
XCS Referenz-Analyse Script
Analysiert alle 36 Produktions-XCS-Dateien systematisch
"""

import os
import re
from collections import defaultdict
from pathlib import Path

def analyze_xcs_files():
    refs_dir = Path("~/projects/rh_caminterface/tests/references").expanduser()
    xcs_files = list(refs_dir.glob("*.xcs"))
    
    print(f"Found {len(xcs_files)} XCS files")
    
    # Data structures for analysis
    command_counts = defaultdict(int)
    all_commands = []
    headers = []
    footers = []
    workpiece_boxes = []
    workplanes = []
    tech_codes = set()
    tool_diameters = []
    drill_depths = []
    macro_types = defaultdict(int)
    variables = []
    setup_positions = []
    
    for xcs_file in sorted(xcs_files):
        print(f"\nAnalyzing: {xcs_file.name}")
        
        with open(xcs_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Store first 10 lines as header pattern
        headers.append({
            'file': xcs_file.name,
            'header': lines[:10] if len(lines) >= 10 else lines
        })
        
        # Store last 10 lines as footer pattern
        footers.append({
            'file': xcs_file.name,
            'footer': lines[-10:] if len(lines) >= 10 else lines
        })
        
        # Analyze each line
        for line_num, line in enumerate(lines, 1):
            line = line.strip()
            if not line or line.startswith('//'):
                continue
                
            # Extract main command
            if '(' in line and not line.startswith('//'):
                cmd = line.split('(')[0]
                command_counts[cmd] += 1
                all_commands.append({
                    'file': xcs_file.name,
                    'line': line_num,
                    'command': cmd,
                    'full_line': line
                })
                
                # Specific pattern analysis
                if cmd == 'CreateFinishedWorkpieceBox':
                    workpiece_boxes.append({
                        'file': xcs_file.name,
                        'line': line
                    })
                
                elif cmd == 'SelectWorkplane':
                    match = re.search(r'SelectWorkplane\("([^"]+)"\)', line)
                    if match:
                        workplanes.append(match.group(1))
                
                elif cmd == 'CreateWorkplane':
                    workplanes.append(f"Custom: {line}")
                
                elif 'E0' in line:  # Tech codes
                    tech_matches = re.findall(r'["\']E\d+["\']', line)
                    for match in tech_matches:
                        tech_codes.add(match.strip('"\''))
                
                elif cmd in ['CreateDrill']:
                    # Extract tool diameter and depth
                    drill_match = re.search(r'CreateDrill\("[^"]+",[\d.-]+,[\d.-]+,([\d.-]+),([\d.-]+)', line)
                    if drill_match:
                        depth = float(drill_match.group(1))
                        diameter = float(drill_match.group(2))
                        drill_depths.append(depth)
                        tool_diameters.append(diameter)
                
                elif cmd == 'CreateMacro':
                    # Extract macro type
                    macro_match = re.search(r'CreateMacro\("[^"]+","([^"]+)"', line)
                    if macro_match:
                        macro_type = macro_match.group(1)
                        macro_types[macro_type] += 1
                
                elif cmd == 'SetWorkpieceSetupPosition':
                    setup_positions.append({
                        'file': xcs_file.name,
                        'line': line
                    })
            
            # Variables
            if line.startswith('double '):
                variables.append({
                    'file': xcs_file.name,
                    'line': line
                })
    
    return {
        'command_counts': command_counts,
        'all_commands': all_commands,
        'headers': headers,
        'footers': footers,
        'workpiece_boxes': workpiece_boxes,
        'workplanes': workplanes,
        'tech_codes': tech_codes,
        'tool_diameters': tool_diameters,
        'drill_depths': drill_depths,
        'macro_types': macro_types,
        'variables': variables,
        'setup_positions': setup_positions,
        'total_files': len(xcs_files)
    }

if __name__ == '__main__':
    results = analyze_xcs_files()
    
    print("\n" + "="*60)
    print("XCS REFERENCE ANALYSIS SUMMARY")
    print("="*60)
    
    print(f"\nTotal files analyzed: {results['total_files']}")
    
    print(f"\nCommand Frequency (Top 20):")
    for cmd, count in sorted(results['command_counts'].items(), key=lambda x: x[1], reverse=True)[:20]:
        print(f"  {cmd:<30} {count:>4}")
    
    print(f"\nTech Codes found:")
    for tech in sorted(results['tech_codes']):
        print(f"  {tech}")
    
    print(f"\nMacro Types:")
    for macro_type, count in sorted(results['macro_types'].items(), key=lambda x: x[1], reverse=True):
        print(f"  {macro_type:<20} {count:>4}")
    
    print(f"\nWorkplanes used:")
    workplane_counts = defaultdict(int)
    for wp in results['workplanes']:
        workplane_counts[wp] += 1
    for wp, count in sorted(workplane_counts.items(), key=lambda x: x[1], reverse=True):
        print(f"  {wp:<30} {count:>4}")
    
    print(f"\nTool Diameters (unique):")
    unique_diameters = sorted(set(results['tool_diameters']))
    print(f"  {unique_diameters}")
    
    print(f"\nDrill Depths (range):")
    if results['drill_depths']:
        print(f"  Min: {min(results['drill_depths']):.1f}, Max: {max(results['drill_depths']):.1f}")
        depth_counts = defaultdict(int)
        for depth in results['drill_depths']:
            depth_counts[depth] += 1
        print("  Common depths:")
        for depth, count in sorted(depth_counts.items(), key=lambda x: x[1], reverse=True)[:10]:
            print(f"    {depth:.1f}mm: {count} times")