# ifc2mct
The project **ifc2mct** is part of my Master's thesis, where an IFC4x1-based method to describe alignment-based steel plate-girder (box-grder) bridge and its mapping to MCT (midas command text) are both presented. To follow the dissertation, this project is also divided into two parts, one is to provide interfaces to produce the bridge's data in the form of .ifc, the other is to translate the built data to mct file which can be imported to Midas Civil.

# Part 1

This sub-project, which is called **ifc2mct.BridgeFactory**, aims to provide a uniform interface (**BridgeBuilder**) to **Application layer** so that any application in this layer is able to produce IFC data of a continus steel-box-girder bridge. The foundation of this project is [XbimEssentials](https://github.com/xBimTeam/XbimEssentials) library, which provides basic functions to access and produce IFC data. **AlignmentBuilder** is a built-in class to create an instance of entity IfcAlignment that has a simple alignment curve. This module is not necessary when **Application layer** or other sources can provide alignment information, for example, IFC files exported from Civil3D. **IfcModelBuilder** is an encapsulation of some interfaces of XbimEssentials in order to quickly construct IFC entity from basic geometric data structure (point, line, matrix, etc). The overall architecture is shown below:

![bridge-factory-architecture](./Images/bridge-factory-architecture.png)

The table below illustrates the interfaces provided by **ifc2mct.BridgeFactory**.
Name | Arguments | Function
---- | --------- | --------
Constructor | path(String) | input ifc file
Build | - | build the IFC bridge model
Save | path(String) | save IFC model to appointed path
SetBridgeAlignment | startPosition(Double), endPosition(Double), offsetVertical(Double), offsetLateral(Double) | 
SetGaps | startGap(Double), endGap(Double) |
SetOverallSection | dimensions(Double[5]) |
SetThickness | plateCode(Integer), distanceAlong(Double), thickness(Double)

In the IFC4x1-based bridge representation presented, bridge alignment curve is an offset curve of the overall alignment curve as shown below:

![bridge-alignment](Images/bridge-alignment.png)

**IfcBearing** is a newly added entity in IFC4x2 draft, so an instance of **IfcProxy** is used to replace it and a property set *Pset_BearingCommon* is assigned. Its positioning depends on the entity **IfcLinearPlacement**, in case most vendors haven't supported this IFC4x1 entity, the arribute *CartesianPosition* should be assigned for backward compatibility.

![bearings](Images/bearings.png)

Flanges and webs are the most important components in a box-girder so they should better be represented by individual entities and then aggregated into an assembly which represents the girder. Some IFC4x2 features (say new *PredefinedType*) are adopted. **IfcSectionedSolidHorizontal** is selected to represent these linear element's shape along the alignment.

![girder](Images/girder.png)

Stiffenrs are part of their parent plates (flanges or webs), so they are aggregated into an instance of **IfcElementAssembly**. 

![stiffeners](Images/stiffeners.png)

Currently, [XbimGeometry](https://github.com/xBimTeam/XbimGeometry) library hasn't adapted to IFC4x1. To enable the visualization, the source code is reviewed and updated to comput the geometry of **IfcAlignmentCurve**, **IfcSectionedSolidHorizontal** and **IfcLinearPlacement**. For now, it's not a good implementation and can only work in a particular situation when the horizontal alignment curve contains only one line segment or arc segment.

![bridge-circular](Images/bridge-circular.png)
![bridge-straight](Images/bridge-straight.png)

# Part 2

To be continued..
